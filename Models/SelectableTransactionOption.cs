using System.ComponentModel;

namespace FinancialTracker.Models;

public sealed class SelectableTransactionOption(
    TransactionOption option,
    bool isSelected) : INotifyPropertyChanged
{
    private bool isSelected = isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TransactionOption Option { get; } = option;
    public string Key => Option.Key;
    public string Title => Option.Title;
    public string IconAsset => Option.IconAsset;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }

            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
