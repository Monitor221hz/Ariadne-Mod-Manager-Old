using Avalonia.Controls;
using Avalonia.Controls.DataGridSorting;

namespace Ariadne.ModManager.GUI.Views;

public sealed class HierarchicalSortingAdapterFactory : IDataGridSortingAdapterFactory
{
    public DataGridSortingAdapter Create(DataGrid grid, ISortingModel model) => new Adapter(model);

    private sealed class Adapter(ISortingModel model)
        : DataGridSortingAdapter(model, static () => [], null, null)
    {
        protected override bool TryApplyModelToView(
            IReadOnlyList<SortingDescriptor> descriptors,
            IReadOnlyList<SortingDescriptor> previousDescriptors,
            out bool changed
        )
        {
            changed = true;
            return true;
        }
    }
}
