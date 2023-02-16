using System;
using IdentityModel.Client;
using System.Net.Http;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;
using Newtonsoft.Json;


namespace RKNet_CashClient.Models
{
    public static class ApiRequest
    {
        public static string Host = ConfigurationManager.AppSettings["apiHost"];
        public static string TokenUrl = "/connect/token";
        public static string ClientId = "RKNetCashClients";
        public static string Secret = "Secret+Clients=2022";

        private static TokenResponse tokenResponse;
        private static DateTime tokenExpiration;               

        //-----------------------------------------------------------------------
        // Получение токена авторизации
        //-----------------------------------------------------------------------
        private static string OAuth2Token()
        {
            if (tokenResponse == null)
            {
                tokenResponse = new OAuth2Client().GetToken();
                tokenExpiration = DateTime.Now.AddSeconds(tokenResponse.ExpiresIn);
            }
            else
            {
                if (DateTime.Now >= tokenExpiration)
                {
                    tokenResponse = new OAuth2Client().GetToken();
                    tokenExpiration = DateTime.Now.AddSeconds(tokenResponse.ExpiresIn);
                }
            }
            return tokenResponse.AccessToken;
        }

        private class OAuth2Client
        {
            public TokenResponse GetToken()
            {
                using (var client = new HttpClient())
                {
                    var tokenResponse = client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
                    {
                        Address = ApiRequest.Host + ApiRequest.TokenUrl,
                        //Scope = apiScope,
                        ClientId = ApiRequest.ClientId,
                        ClientSecret = ApiRequest.Secret
                    });
                    return tokenResponse.Result;
                }                
            }
        }
        
        //-----------------------------------------------------------------------
        // Кассовые клиенты
        //-----------------------------------------------------------------------        

        // получение информации по кассовому клиенту
        public static RKNet_Model.Result<RKNet_Model.CashClient.CashClient> GetClientInfo(string clientId)
        {
            var result = new RKNet_Model.Result<RKNet_Model.CashClient.CashClient>();
            var requestUrl = Host + "/CashClients/GetClientInfo?clientId=" + clientId;

            using (var client = new HttpClient())
            {
                client.SetBearerToken(OAuth2Token());
                var response = client.GetAsync(requestUrl).Result;

                // ошибка авторизации (если по какой-то причине токен перестал быть актуальным, например, перезагрузка сервера)
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    tokenResponse = null;
                    client.SetBearerToken(OAuth2Token());
                    response = client.GetAsync(requestUrl).Result;
                }

                // разбираем ответ
                if (response.IsSuccessStatusCode)
                {
                    var json = response.Content.ReadAsStringAsync().Result;
                    result = Newtonsoft.Json.JsonConvert.DeserializeObject<RKNet_Model.Result<RKNet_Model.CashClient.CashClient>>(json);
                }
                else
                {
                    result.Ok = false;
                    result.ErrorMessage = response.StatusCode.ToString();
                }
            }
            return result;
        }   

        // скачивание файла обновления (для клиентов до версии 1.2.85) - удалить
        public static RKNet_Model.Result<RKNet_Model.CashClient.ClientVersion> GetCashClientVersionFile(string version)
        {
            var result = new RKNet_Model.Result<RKNet_Model.CashClient.ClientVersion>();
            var requestUrl = Host + "/CashClients/GetCashClientVersionFile?version=" + version;

            using (var client = new HttpClient())
            {
                client.SetBearerToken(OAuth2Token());
                var response = client.GetAsync(requestUrl).Result;
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        // если читать json строкой, то на больших фалйлах возникает перегрузка памяти, особенно на 32 битных системах
                        //var json = response.Content.ReadAsStringAsync().Result;
                        //result = Newtonsoft.Json.JsonConvert.DeserializeObject<RKNet_Model.Result<RKNet_Model.CashClient.ClientVersion>>(json);


                        // поэтому читаем json стримом                        
                        using (var streamReader = new StreamReader(response.Content.ReadAsStreamAsync().Result))
                        {
                            using (var reader = new JsonTextReader(streamReader))
                            {
                                var serializer = new JsonSerializer();

                                // read the json from a stream
                                // json size doesn't matter because only a small piece is read at a time from the HTTP request 
                                result = serializer.Deserialize<RKNet_Model.Result<RKNet_Model.CashClient.ClientVersion>>(reader);
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        result.Ok = false;
                        result.ErrorMessage = $"ошибка десериализации файла обновления: {ex.ToString()}";
                    }

                }
                else
                {
                    result.Ok = false;
                    result.ErrorMessage = response.StatusCode.ToString();
                }
            }
            return result;
        }

        // скачивание файла обновления (работает с версии 1.2.85)
        public static async Task<RKNet_Model.Result<byte[]>> GetUpdateFile(string version)
        {
            var result = new RKNet_Model.Result<byte[]>();
            var requestUrl = Host + "/CashClients/GetCashClientUpdateFile?version=" + version;

            try
            {
                using (var client = new HttpClient())
                {
                    client.SetBearerToken(OAuth2Token());
                    using (var data = client.GetByteArrayAsync(requestUrl))
                    {
                        if(data.Result.Length > 0)
                        {
                            result.Data = data.Result;
                        }
                        else
                        {
                            result.Ok = false;
                            result.ErrorMessage = $"файл обновления отсутствует на сервере";
                        }                        
                    }
                }
            }
            catch(Exception ex)
            {
                result.Ok = false;
                result.ErrorMessage = $"{ex.Message}";
            }
            
            return result;
        }
    }    
}
