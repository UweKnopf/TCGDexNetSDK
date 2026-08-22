using System.Collections;
using System.Net;
using System.Reflection;
using net_sdk.src.internal_classes;
using net_sdk.src.models;
using RestSharp;

namespace net_sdk.src;


public class TCGDex: IDisposable
{

    internal struct CacheValue
    {
        internal Object Data;
        internal DateTime DateTime;
    }
    private readonly RestClient _client;
    private readonly Dictionary<string, CacheValue> cache = new Dictionary<string, CacheValue>();
    private double TTL = 30;

    /// <summary>
    /// Initializes a new instance of the TCGDex class with the specified language. The language parameter determines the language of the data returned by the API. 
    /// Supported languages are for example "en" (English), "de" (German) or "fr" (French). 
    /// If no language is specified, it defaults to English ("en").
    /// </summary>
    /// <param name="language"></param>
    public TCGDex(string language = "en")
    {
        var options = new RestClientOptions($"https://api.tcgdex.net/v2/{language}");
        _client = new RestClient(options);
        _client.AddDefaultHeader("user-agent", "@UweKnopf/net-sdk");
    }

    private object? CacheLookUp(string relativePath)
    {
        if (cache.TryGetValue(relativePath, out CacheValue value) && DateTime.Now.Subtract(value.DateTime).TotalMinutes < TTL)
        {
            return value.Data;
        }

        return null;
    }

    /// <summary>
    /// Sets the time in minutes before a cache entry is discarded and fetched again.
    /// Default is 30 minutes.
    /// </summary>
    /// <param name="TTLInMinutes"></param>
    public void SetCacheTTL(double TTLInMinutes)
    {
        TTL = TTLInMinutes;
    }

    private async Task<T> Fetch<T>(string fetchParam) where T : Model
    {
        var cachedData = CacheLookUp(fetchParam);
        if (cachedData != null)
        {
            return (T)cachedData;
        }

        var req = new RestRequest(fetchParam);
        var response = await _client.GetAsync<T>(req);

        //Should not be null because GetAsync would throw
        response!.TCGDex = this;
        //there must be a better way
        PropertyInfo[] properties = typeof(T).GetProperties();
        foreach (PropertyInfo property in properties)
        {
            if (property.GetValue(response) is IEnumerable l)
            {
                IEnumerable<Model> items = l.OfType<Model>();
                if (items.Count() != 0)
                {
                    foreach (var item in items)
                    {
                        item.TCGDex = this;
                    }
                }
            }
            if (property.GetValue(response) != null && typeof(Model).IsAssignableFrom(property.PropertyType))
            {
                var x = (Model)property.GetValue(response)!;
                x.TCGDex = this;
                property.SetValue(response, x);
                
            }
            
        }

        cache[fetchParam] = new CacheValue
        {
            Data = response,
            DateTime = DateTime.Now
        };

        return response;
    }

    private async Task<List<T>> FetchList<T>(string fetchParam) where T : Model
    {
        var cachedData = CacheLookUp(fetchParam);
        if (cachedData != null)
        {
            return (List<T>)cachedData;
        }
        
        RestRequest req = new RestRequest(fetchParam);
        try
        {
            var response = await _client.GetAsync<List<T>>(req);
            foreach (var card in response!)
        {
            card.TCGDex = this;
        }

        cache[fetchParam] = new CacheValue
        {
            Data = response,
            DateTime = DateTime.Now
        };

        return response;

        }
        catch (Exception)
        {
            Console.WriteLine($"Error fetching list of {typeof(T).Name} from {fetchParam}");
            return null;
        }
        
    }

    private async Task<List<T>> FetchList<T>(string fetchParam, Query query) where T : Model
    {
        var combinedRequestURL = query.ReturnRequestStringWithAllQueries(fetchParam);
        var cachedData = CacheLookUp(combinedRequestURL);
        if (cachedData != null)
        {
            return (List<T>)cachedData;
        }

        RestRequest req = new RestRequest(combinedRequestURL);
        
        var response = await _client.GetAsync<List<T>>(req);
        foreach (var element in response!)
        {
            element.TCGDex = this;
        }

        cache[combinedRequestURL] = new CacheValue
        {
            Data = response,
            DateTime = DateTime.Now
        };

        return response;
    }

    private async Task<List<T>> FetchSimpleList<T>(string fetchParam)
    {
        var cachedData = CacheLookUp(fetchParam);
        if (cachedData != null)
        {
            return (List<T>)cachedData;
        }

        var req = new RestRequest(fetchParam);
        /*
        This is probably not the way to do this but we first need to think about what (if any) errors we want to return to caller.
        Might be better to not use getasync and instead separate response and deserialization

        In that case the error thrown from restsharp would be more meaningful or, if none is thrown, we could do it ourself
        */
        
        var response = await _client.GetAsync<List<T>>(req);

        cache[fetchParam] = new CacheValue
        {
            //cant be null because GetAsync would throw before that
            Data = response!,
            DateTime = DateTime.Now
        };

        return response!;
    }

    /// <summary>
    /// Get any asset (e.g. images, logos, symbols) of the api as bytes.
    /// </summary>
    /// <param name="imageUrl"></param>
    /// <returns>A byte array containing the image data.</returns>
    public async Task<byte[]?> GetImage(string imageUrl)
    {
        var fileBytes = await _client.DownloadDataAsync(new RestRequest(imageUrl, Method.Get));
        return fileBytes;
    }

    /// <summary>
    /// Disposes the RestClient and suppresses finalization. Should be called when you are done using the TCGDex instance.
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Async queries a list of <see cref="CardResume"/>.
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public async Task<List<CardResume>?> FetchCards(Query query)
    {
        var response = await FetchList<CardResume>("/cards", query);
        return response;
    }

    /// <summary>
    /// Async returns a list of all <see cref="CardResume"/>.
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public async Task<List<CardResume>?> FetchCards()
    {
        var response = await FetchList<CardResume>("/cards");
        return response;
    }

    /// <summary>
    /// Async returns a <see cref="Card"/> based on its id property.
    /// </summary>
    /// <param name="cardId"></param>
    /// <returns></returns>
    public async Task<Card> FetchCard(string cardId)
    {
        var response = await Fetch<Card>("/cards/" + cardId);
        return response;
    }


    /// <summary>
    /// Async returns a <see cref="Set"/> based on its id property.
    /// </summary>
    /// <param name="setId"></param>
    /// <returns></returns>
    public async Task<Set> FetchSet(string setId)
    {
        var response = await Fetch<Set>("/sets/" + setId);
        return response;
    }

    /// <summary>
    /// Async queries a list of <see cref="SetResume"/>.
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public async Task<List<SetResume>> FetchSets(Query query)
    {
        var response = await FetchList<SetResume>("/sets", query);
        return response;
    }

    /// <summary>
    /// Async returns a list of all <see cref="SetResume"/>.
    /// </summary>
    /// <returns></returns>
    public async Task<List<SetResume>> FetchSets()
    {
        var response = await FetchList<SetResume>("/sets");
        return response;
    }

    /// <summary>
    /// Async returns a <see cref="Serie"/> based on its id property.
    /// </summary>
    /// <param name="serieId"></param>
    /// <returns></returns>
    public async Task<Serie> FetchSerie(string serieId)
    {
        var response = await Fetch<Serie>("/series/" + serieId);
        return response;
    }

    /// <summary>
    /// Async queries a list of <see cref="Serie"/>.
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public async Task<List<Serie>> FetchSeries(Query query)
    {
        var response = await FetchList<Serie>("/series", query);
        return response;
    }

    /// <summary>
    /// Async returns a list of all <see cref="Serie"/>.
    /// </summary>
    /// <returns></returns>
    public async Task<List<Serie>> FetchSeries()
    {
        var response = await FetchList<Serie>("/series");
        return response;
    }

    /// <summary>
    /// Async returns a list of all types, e.g. "Fire", "Water", "Grass", etc.
    /// </summary>
    /// <returns></returns>
    public async Task<List<string>> FetchTypes()
    {
        var response = await FetchSimpleList<string>("/types");
        return response;
    }

    /// <summary>
    /// Async returns a list of all rarities, e.g. "Common", "Uncommon", "Rare", etc.
    /// </summary>
    /// <returns></returns>
    public async Task<List<string>> FetchRarities()
    {
        var response = await FetchSimpleList<string>("/rarities");
        return response;
    }

    /// <summary>
    /// Async returns a list of all illustrators, e.g. "Ken Sugimori", "Mitsuhiro Arita", etc.
    /// </summary>
    /// <returns></returns>
    public async Task<List<string>> FetchIllustrators()
    {
        var response = await FetchSimpleList<string>("/illustrators");
        return response;
    }

    /// <summary>
    /// Async returns a list of all categories, e.g. "Pokemon", "Trainer", "Energy", etc.
    /// </summary>
    /// <returns></returns>
    public async Task<List<string>> FetchCategories()
    {
        var response = await FetchSimpleList<string>("/categories");
        return response;
    }

    /// <summary>
    /// Async returns a list of all stages, e.g. "Basic", "Stage 1", "Stage 2", etc.
    /// </summary>
    /// <returns></returns>
    public async Task<List<string>> FetchStages()
    {
        var response = await FetchSimpleList<string>("/stages");
        return response;
    }

    /// <summary>
    /// Async returns a list of all suffixes, e.g. "V", "VMAX", "GX", etc.
    /// </summary>
    /// <returns></returns>
    public async Task<List<string>> FetchSuffixes()
    {
        var response = await FetchSimpleList<string>("/suffixes");
        return response;
    }

    /// <summary>
    /// Async returns a list of all variants, e.g. "Normal", "Reverse Holo", "Holo", etc.
    /// </summary>
    /// <returns></returns>
    public async Task<List<string>> FetchVariants()
    {
        var response = await FetchSimpleList<string>("/variants");
        return response;
    }
}
