namespace CompartiBici.Services;

public interface IAlgoliaSearchService
{
    Task<List<int>> BuscarIncidenciasAsync(string query);
}
