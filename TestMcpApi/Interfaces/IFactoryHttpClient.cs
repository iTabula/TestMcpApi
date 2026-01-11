using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestMcpApi.Interfaces
{
    public interface IFactoryHttpClient
    {
        Task<T> GetRequest<T>(string WebApiName, string command, HttpContent content, string AccessToken = "", string ApiVersion = "1.0");
        Task<T> PostRequest<T>(string WebApiName, string command, HttpContent content, string AccessToken = "", string ApiVersion = "1.0");
        Task<T> DeleteRequest<T>(string WebApiName, string command, HttpContent content, string AccessToken = "", string ApiVersion = "1.0");
        StringContent SetHttpContent<T>(T req);
    }
}
