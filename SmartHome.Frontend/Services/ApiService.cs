using SmartHome.Shared.Entities;
using SmartHome.Shared.Request;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartHome.Frontend.Services
{
    public class ApiService(IHttpClientFactory _httpClientFactory)
    {
        private readonly HttpClient client = _httpClientFactory.CreateClient("Backend");

        public async Task<List<MiniDTO>> GetDevices()
        {
            var response = await client.GetAsync("/api/devices");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<MiniDTO>>();
        }

        public async Task<MiniDTO> GetDevice(int id)
        {
            var response = await client.GetAsync($"/api/devices/{id}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MiniDTO>();
        }

        public async Task<Shared.Contracts.OAuthTokenResponse> GetTokenAsync(string usuario, string password)
        {
            var request = new LoginRequest
            {
                Usuario = usuario,
                Password = password
            };
            var response = await client.PostAsJsonAsync("/oauth/login", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Shared.Contracts.OAuthTokenResponse>();
        }
    }
}

