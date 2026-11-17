/*
 * Checkbox view model for choosing which of the catalog's collections to search.
 */
using ArcGIS.Desktop.Framework.Contracts;
using StaclidarAddin.Stac;

namespace StaclidarAddin
{
    /// <summary>A checkable STAC collection entry in the collections list.</summary>
    public class CollectionCheckViewModel : PropertyChangedBase
    {
        private bool _isChecked;

        public CollectionCheckViewModel(StacCollection collection, StacApiSource source)
        {
            Collection = collection;
            Source = source;
            Id = collection.Id;
            Title = collection.TitleOrId;
        }

        public StacCollection Collection { get; }
        public StacApiSource Source { get; }
        public string Id { get; }
        public string Title { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value, () => IsChecked);
        }

        public override string ToString() => $"{Title} ({Id})";
    }
}
