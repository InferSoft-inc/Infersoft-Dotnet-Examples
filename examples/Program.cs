using Examples.Model;
using System.Net.Http;
using System.Text;
using System.Text.Json;
namespace Examples
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            HttpClient httpClient = new HttpClient();
            var token = await GetCredentials(httpClient);
            if (token == null) {
                throw new Exception("Failed to get authentication token.");
            }
            // Set up
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            httpClient.BaseAddress = new Uri("https://api.infersoft.com/api/");
            // Let's upload some files
            // We need to know their names and sizes ahead of time
            // In the repository we have 3 pdfs
            // We start by fetching the presigned URL from Infersoft API

            var pdfFiles = new List<string>
            {
                "docs/file1.pdf",
                "docs/file2.pdf",
                "docs/file3.pdf"
            };

            var uploadRequest = BuildUploadRequestFromFiles(pdfFiles);
            var uploadRequestJson = JsonSerializer.Serialize(uploadRequest);
            var uploadContent = new StringContent(uploadRequestJson, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));
            // Make the request to get presigned URLs
            HttpResponseMessage uploadResponseMessage = await httpClient.PostAsync("uploads", uploadContent);
            uploadResponseMessage.EnsureSuccessStatusCode();
            var responseJson = await uploadResponseMessage.Content.ReadAsStringAsync();
            var uploadResponse = JsonSerializer.Deserialize<UploadResponse>(
                responseJson,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );
            // Now we have the presigned URLs and the names of the files that will be uploaded
            // We can proceed to upload each file to its corresponding presigned URL
            // In here we have to inject the required headers returned by the API
            for ( int i = 0; i < pdfFiles.Count; i++)
            {
                var pdfFile = pdfFiles[i];
                var uploadItem = uploadResponse.Items[i];
                // Insert required headers into client
                httpClient.DefaultRequestHeaders.Clear();
                foreach (var header in uploadItem.RequiredHeaders)
                {
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }

                Console.WriteLine($"Uploading {pdfFile} to {uploadItem.PutUrl}");
                await UploadFileAsync(
                    httpClient,
                    uploadItem.PutUrl,
                    pdfFile,
                    new string[] {
                        "Content-Type: application/pdf"
                    }
                );
                Console.WriteLine($"Uploaded {pdfFile} successfully.");
            }
            // All files uploaded, let's send these files to classify and extract

        }

        private async static Task<string?> GetCredentials(HttpClient httpClient)
        {
            // Get your authentication token
            var clientId = Environment.GetEnvironmentVariable("AUTH0_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("AUTH0_CLIENT_SECRET");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                Console.WriteLine("Missing AUTH0_CLIENT_ID or AUTH0_CLIENT_SECRET environment variables.");
                return null;
            }
            httpClient.BaseAddress = new Uri("https://dev-noabnisxxguu0jp0.us.auth0.com");
            var requestBody = new
            {
                grant_type = "client_credentials",
                client_id = clientId,
                client_secret = clientSecret,
                audience = "https://api.infersoft.com"
            };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");


            var response = await httpClient.PostAsync("/oauth/token", content);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            var token = doc.RootElement.GetProperty("access_token").GetString();
            return token;
        }
        private async static Task UploadFileAsync(HttpClient client, string presignedUrl, string filePath, string[] headers)
        {

            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);

            using var content = new ByteArrayContent(fileBytes);
            foreach (var header in headers)
            {
                var splitHeader = header.Split(':');
                if (splitHeader.Length == 2)
                {
                    content.Headers.Add(splitHeader[0].Trim(), splitHeader[1].Trim());
                }
            }
            using var request = new HttpRequestMessage(HttpMethod.Put, presignedUrl)
            {
                Content = content
            };

            using HttpResponseMessage response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string respBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Upload failed. Status: {response.StatusCode}, Body: {respBody}");
            }
        }

        private static UploadRequest BuildUploadRequestFromFiles(IEnumerable<string> pdfFilePaths)
        {
            var uploadRequest = new UploadRequest
            {
                Files = pdfFilePaths.Select(path => new UploadFile
                {
                    ContentType = "application/pdf",
                    FileName = Path.GetFileName(path),
                    Size = new FileInfo(path).Length
                }).ToList()
            };

            return uploadRequest;
        }

    }
}