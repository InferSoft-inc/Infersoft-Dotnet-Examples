using examples.Model;
using Examples.Model;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Examples
{
    internal class Program
    {
        private const string Auth0BaseUrl = "https://dev-noabnisxxguu0jp0.us.auth0.com";
        private const string ApiBaseUrl = "https://api.infersoft.com/api/";

        static async Task Main(string[] args)
        {
            using var authClient = new HttpClient { BaseAddress = new Uri(Auth0BaseUrl) };
            using var apiClient = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };

            // Get your authentication token
            var token = await GetAccessTokenAsync(authClient);
            if (token == null)
            {
                throw new Exception("Failed to get authentication token.");
            }

            // Set up
            apiClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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

            // Make the request to get presigned URLs
            var uploadResponse = await PostJsonAsync<UploadRequest, UploadResponse>(apiClient, "uploads", uploadRequest);
            if (uploadResponse == null || uploadResponse.Items.Count != pdfFiles.Count)
                throw new Exception("Upload response is invalid or does not match the number of files.");

            // Now we have the presigned URLs and the names of the files that will be uploaded
            // We can proceed to upload each file to its corresponding presigned URL
            // In here we have to inject the required headers returned by the API
            for (int i = 0; i < pdfFiles.Count; i++)
            {
                var pdfFile = pdfFiles[i];
                var uploadItem = uploadResponse.Items[i];

                Console.WriteLine($"Uploading {pdfFile} to {uploadItem.PutUrl}");

                await UploadFileAsync(
                    uploadItem.PutUrl,
                    pdfFile,
                    uploadItem.RequiredHeaders
                );

                Console.WriteLine($"Uploaded {pdfFile} successfully.");
            }

            // All files uploaded, let's send these files to classify and extract
            // We'll use a NameSelector here but the API will improve in the future
            // We need to get credits estimate before sending the job
            // Then we can approve and send the job with the proposed budget ID
            var selectors = uploadResponse.Items
                .Select(i => new NameSelector { Name = i.ClientFileName })
                .ToArray();

            var budgetRequest = new EstimateCreditsRequest
            {
                Prompts = new[] { 1, 2, 3, 4 },
                Selectors = selectors,
                Steps = new[] { "classify", "extract" },
                Synchronous = false
            };

            var creditsEstimate = await PostJsonAsync<EstimateCreditsRequest, EstimateCreditsResponse>(
                apiClient,
                "job/credits/estimate",
                budgetRequest
            ) ?? throw new Exception("Budget response was null.");
            // Display whole estimate response
            Console.WriteLine($"Estimated credits for job: {creditsEstimate.TotalCredits} over {creditsEstimate.PageCount} pages. ID for Estimate {creditsEstimate.Id}");
        }

        // ----------------------------------------------------
        //              Helper Methods
        // ----------------------------------------------------

        private static async Task<string?> GetAccessTokenAsync(HttpClient client)
        {
            // Get your authentication token
            var clientId = Environment.GetEnvironmentVariable("AUTH0_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("AUTH0_CLIENT_SECRET");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                Console.WriteLine("Missing AUTH0_CLIENT_ID or AUTH0_CLIENT_SECRET environment variables.");
                return null;
            }

            var requestBody = new
            {
                grant_type = "client_credentials",
                client_id = clientId,
                client_secret = clientSecret,
                audience = "https://api.infersoft.com"
            };

            var response = await client.PostAsJsonAsync("/oauth/token", requestBody);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("access_token").GetString();
        }

        /// <summary>
        /// Generic helper to POST JSON and parse JSON response.
        /// Avoids repetitive boilerplate everywhere.
        /// </summary>
        private static async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(
            HttpClient client,
            string url,
            TRequest body)
        {
            var response = await client.PostAsJsonAsync(url, body);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<TResponse>(
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );
        }

        /// <summary>
        /// Uploads a file directly to a presigned URL.
        /// Injects required headers returned by the API.
        /// </summary>
        private static async Task UploadFileAsync(
            string presignedUrl,
            string filePath,
            IDictionary<string, string> requiredHeaders)
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);

            using var content = new ByteArrayContent(fileBytes);
            content.Headers.Add("Content-Type", "application/pdf");

            using var request = new HttpRequestMessage(HttpMethod.Put, presignedUrl)
            {
                Content = content
            };

            // Insert required headers into the request
            foreach (var header in requiredHeaders)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            using var uploadClient = new HttpClient();
            var response = await uploadClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var respBody = await response.Content.ReadAsStringAsync();
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
