using System.Text;
using System.Text.Json;
using TestMcpApi.Interfaces;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace TestMcpApi.Services
{
    public class FactoryHttpClient : IFactoryHttpClient
    {
        private readonly ILogger<FactoryHttpClient> _logger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions options = new JsonSerializerOptions()
        {
            //MaxDepth = 2,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            IgnoreReadOnlyProperties = true,
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve
        };
        public FactoryHttpClient(ILogger<FactoryHttpClient> logger, IHttpClientFactory clientFactory, IConfiguration configuration)
        {
            _clientFactory = clientFactory;
            _configuration = configuration;
            _logger = logger;
        }
        public async Task<T> GetRequest<T>(string WebApiName, string command, HttpContent content, string AccessToken = "",
            string ApiVersion = "1.0")
        {
            var client = _clientFactory.CreateClient(WebApiName);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("X-Version", ApiVersion);
            if (!string.IsNullOrEmpty(AccessToken))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            }
            string RequestUri = $"{client.BaseAddress}{command}";
            try
            {
                var _cancelTokenSource = new CancellationTokenSource();
                var _cancelToken = _cancelTokenSource.Token;
                var resp = await client.GetAsync(RequestUri, _cancelToken);
                T? data = await resp.Content.ReadFromJsonAsync<T>(options, _cancelToken);
                return data!;
            }
            catch (Exception e)
            {
                _logger.LogDebug($"GetRequest() url=[{client.BaseAddress}{command}] error=[{e.Message} - {e.StackTrace}]");
                return (T)Activator.CreateInstance(typeof(T))!;
            }
        }
        public async Task<T> PostRequest<T>(string WebApiName, string command, HttpContent content, string AccessToken = "",
            string ApiVersion = "1.0")
        {
            var client = _clientFactory.CreateClient(WebApiName);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("X-Version", ApiVersion);
            if (!string.IsNullOrEmpty(AccessToken))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            }

            string RequestUri = $"{client.BaseAddress}{command}";

            try
            {
                var _cancelTokenSource = new CancellationTokenSource();
                var _cancelToken = _cancelTokenSource.Token;
                var resp = await client.PostAsync(RequestUri, content, _cancelToken);
                T? data = await resp.Content.ReadFromJsonAsync<T>(options, _cancelToken);
                return data!;
            }
            catch (Exception e)
            {
                _logger.LogDebug($"PostRequest() url=[{client.BaseAddress}{command}] error=[{e.Message} - {e.StackTrace}]");

                return (T)Activator.CreateInstance(typeof(T))!;
            }
        }

        public async Task<T> DeleteRequest<T>(string WebApiName, string command, HttpContent content, string AccessToken = "",
            string ApiVersion = "1.0")
        {
            var client = _clientFactory.CreateClient(WebApiName);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("X-Version", ApiVersion);
            if (!string.IsNullOrEmpty(AccessToken))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            }

            string RequestUri = $"{client.BaseAddress}{command}";

            try
            {
                var _cancelTokenSource = new CancellationTokenSource();
                var _cancelToken = _cancelTokenSource.Token;
                var resp = await client.DeleteAsync(RequestUri, _cancelToken);
                T? data = await resp.Content.ReadFromJsonAsync<T>(options, _cancelToken);
                return data!;
            }
            catch (Exception e)
            {
                _logger.LogDebug($"DeleteRequest() url=[{client.BaseAddress}{command}] error=[{e.Message} - {e.StackTrace}]");
                return (T)Activator.CreateInstance(typeof(T))!;
            }
        }

        public StringContent SetHttpContent<T>(T req)
        {
            string reqInfo = JsonSerializer.Serialize(req, options);
            return new StringContent(reqInfo, Encoding.UTF8, "application/json");
        }
    }
}
