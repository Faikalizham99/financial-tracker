using System.Buffers.Binary;
using System.Xml;
using System.Xml.Linq;

namespace FinancialTracker.Services;

public sealed class AppGroupConfigurationException(
    string message,
    Exception? inner = null)
    : InvalidOperationException(message, inner);

// Reads the entitlement blob embedded in installed arm64 executables. iOS still
// enforces signature and provisioning validity; this only lets each process
// select the same group after a sideloading service rewrites entitlements.
public static class SignedAppGroups
{
    public const string PreferredGroup =
        WidgetConstants.PrimaryAppGroupIdentifier;

    private const int MaximumMetadataSize = 1024 * 1024;

    public static HashSet<string> Read(string executable)
    {
        using var stream = File.OpenRead(executable);
        return Read(stream);
    }

    public static HashSet<string> Read(Stream stream)
    {
        byte[] ReadAt(long offset, int length)
        {
            if (offset < 0 ||
                length < 0 ||
                length > MaximumMetadataSize ||
                offset > stream.Length - length)
            {
                throw new InvalidDataException("Invalid code-signature bounds.");
            }

            stream.Position = offset;
            var data = new byte[length];
            stream.ReadExactly(data);
            return data;
        }

        static uint Little(byte[] bytes, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

        static uint Big(byte[] bytes, int offset) =>
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));

        var header = ReadAt(0, 32);
        if (Little(header, 0) != 0xfeedfacf)
        {
            throw new InvalidDataException("Expected an arm64 Mach-O executable.");
        }

        var commandCount = Little(header, 16);
        var commandsSize = Little(header, 20);
        if (commandCount > 4096 || commandsSize > MaximumMetadataSize)
        {
            throw new InvalidDataException("Invalid Mach-O load commands.");
        }

        var commands = ReadAt(32, (int)commandsSize);
        var cursor = 0;
        for (var commandIndex = 0; commandIndex < commandCount; commandIndex++)
        {
            if (cursor > commands.Length - 8)
            {
                throw new InvalidDataException("Truncated Mach-O command.");
            }

            var command = Little(commands, cursor);
            var size = Little(commands, cursor + 4);
            if (size < 8 || size > commands.Length - cursor)
            {
                throw new InvalidDataException("Invalid Mach-O command size.");
            }

            if (command == 0x1d) // LC_CODE_SIGNATURE
            {
                return ReadGroupsFromSignature(
                    stream,
                    ReadAt,
                    commands,
                    cursor,
                    size,
                    Big);
            }

            cursor += (int)size;
        }

        return new HashSet<string>(StringComparer.Ordinal);
    }

    public static string SelectCommon(IEnumerable<HashSet<string>> signatures)
    {
        HashSet<string>? common = null;
        var signatureCount = 0;
        foreach (var groups in signatures)
        {
            if (common is null)
            {
                common = new HashSet<string>(groups, StringComparer.Ordinal);
            }
            else
            {
                common.IntersectWith(groups);
            }

            signatureCount++;
        }

        if (signatureCount != 3 || common is null || common.Count == 0)
        {
            throw new AppGroupConfigurationException(
                "Financial Tracker, its widget, and its Shortcut extension " +
                "do not have a common signed App Group. Re-sign all three " +
                "extensions together in FlareStore.");
        }

        return common.Contains(PreferredGroup)
            ? PreferredGroup
            : common.Order(StringComparer.Ordinal).First();
    }

    private static HashSet<string> ReadGroupsFromSignature(
        Stream stream,
        Func<long, int, byte[]> readAt,
        byte[] commands,
        int cursor,
        uint commandSize,
        Func<byte[], int, uint> big)
    {
        if (commandSize < 16)
        {
            throw new InvalidDataException("Truncated code-signature command.");
        }

        var signatureOffset = (long)BinaryPrimitives.ReadUInt32LittleEndian(
            commands.AsSpan(cursor + 8, 4));
        var signatureSize = BinaryPrimitives.ReadUInt32LittleEndian(
            commands.AsSpan(cursor + 12, 4));
        var signature = readAt(signatureOffset, 12);
        var signatureLength = big(signature, 4);
        var blobCount = big(signature, 8);
        if (big(signature, 0) != 0xfade0cc0 ||
            signatureLength > signatureSize ||
            blobCount > 128 ||
            signatureLength < 12 + blobCount * 8 ||
            signatureOffset > stream.Length - signatureLength)
        {
            throw new InvalidDataException("Invalid code-signature superblob.");
        }

        var entries = readAt(signatureOffset + 12, checked((int)blobCount * 8));
        for (var blobIndex = 0; blobIndex < blobCount; blobIndex++)
        {
            if (big(entries, checked((int)blobIndex * 8)) != 5)
            {
                continue;
            }

            var offset = big(entries, checked((int)blobIndex * 8 + 4));
            if (offset < 12 + blobCount * 8 || offset > signatureLength - 8)
            {
                throw new InvalidDataException("Invalid entitlement offset.");
            }

            var blob = readAt(signatureOffset + offset, 8);
            var length = big(blob, 4);
            if (big(blob, 0) != 0xfade7171 ||
                length < 8 ||
                length > MaximumMetadataSize ||
                length > signatureLength - offset)
            {
                throw new InvalidDataException("Invalid entitlement blob.");
            }

            return ParseGroups(readAt(
                signatureOffset + offset + 8,
                checked((int)length - 8)));
        }

        return new HashSet<string>(StringComparer.Ordinal);
    }

    private static HashSet<string> ParseGroups(byte[] entitlementPlist)
    {
        using var xmlStream = new MemoryStream(entitlementPlist);
        using var reader = XmlReader.Create(
            xmlStream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumMetadataSize
            });
        var dictionary = XDocument.Load(reader).Root?.Element("dict")
            ?? throw new InvalidDataException("Invalid entitlement plist.");
        var key = dictionary.Elements("key").SingleOrDefault(element =>
            element.Value == "com.apple.security.application-groups");
        if (key is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var array = key.ElementsAfterSelf().FirstOrDefault();
        if (array?.Name != "array" ||
            array.Elements().Any(element => element.Name != "string"))
        {
            throw new InvalidDataException(
                "App Groups must be an array of strings.");
        }

        return array.Elements("string")
            .Select(element => element.Value)
            .Where(IsGroupIdentifier)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsGroupIdentifier(string value) =>
        value.StartsWith("group.", StringComparison.Ordinal) &&
        value.Length > 6 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '-' or '_');
}
