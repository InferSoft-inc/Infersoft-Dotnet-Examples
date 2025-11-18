using DotNetEnv;
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
            TryLoadDotEnv();

            using var authClient = new HttpClient { BaseAddress = new Uri(Auth0BaseUrl) };
            using var apiClient = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };

            Console.WriteLine("Starting Infersoft end-to-end workflow demo...");

            // Get your authentication token
            Console.WriteLine("Requesting Auth0 access token...");
            var token = await GetAccessTokenAsync(authClient);
            if (token == null)
            {
                throw new Exception("Failed to get authentication token.");
            }
            Console.WriteLine("Authentication succeeded. Configuring API client.");

            // Set up
            apiClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Let's upload some files
            // We need to know their names and sizes ahead of time
            // In the repository we have 3 pdfs
            // We start by fetching the presigned URL from Infersoft API
            var pdfFiles = new List<string>
            {
                "docs/doc_1.pdf",
                "docs/doc_2.pdf",
                "docs/doc_3.pdf"
            };

            Console.WriteLine("Preparing upload request for local PDF samples...");
            var uploadRequest = BuildUploadRequestFromFiles(pdfFiles);

            // Make the request to get presigned URLs
            Console.WriteLine("Requesting presigned URLs from Infersoft API...");
            Console.WriteLine($"Upload request with headers: {JsonSerializer.Serialize(uploadRequest, new JsonSerializerOptions { WriteIndented = true })}");
            var uploadResponse = await PostJsonAsync<UploadRequest, UploadResponse>(apiClient, "uploads", uploadRequest);
            if (uploadResponse == null || uploadResponse.Items.Count != pdfFiles.Count)
                throw new Exception("Upload response is invalid or does not match the number of files.");
            Console.WriteLine("Received presigned URLs. Beginning uploads.");

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
            var nameSelectors = uploadResponse.Items
                .Select(i => new NameSelector { Name = i.ClientFileName })
                .ToArray();
            Console.WriteLine("Building selectors from uploaded filenames...");
            var selectors = new Selectors
            {
                Include = nameSelectors
            };

            var budgetRequest = new EstimateCreditsRequest
            {
                Prompts = new[] { 1, 2, 3, 4 },
                Selectors = selectors,
                Steps = new[] { "classify", "extract" },
                Synchronous = false
            };

            Console.WriteLine("Requesting credit estimate for classify/extract steps...");
            var creditsEstimate = await PostJsonAsync<EstimateCreditsRequest, EstimateCreditsResponse>(
                apiClient,
                "job/credits/estimate",
                budgetRequest
            ) ?? throw new Exception("Budget response was null.");
            // Display whole estimate response
            Console.WriteLine($"Estimated credits for job: {creditsEstimate.TotalCredits} over {creditsEstimate.PageCount} pages. ID for Estimate {creditsEstimate.Id}");
            // Create a project to run this job under
            Console.WriteLine("Creating example project to host this job...");
            var projectResponse = await PostJsonAsync<CreateProjectRequest, CreateProjectResponse>(
                apiClient,
                "projects",
                new CreateProjectRequest { Name = "Example Project from C# SDK" }
            ) ?? throw new Exception("Project creation failed.");
            // Finally we can submit the job with the budget ID
            Console.WriteLine("Starting job with approved budget and project...");
            var startJobResponse = await PostJsonAsync<StartJobsRequest, StartJobsResponse>(
                apiClient,
                "jobs/start",
                new StartJobsRequest
                {
                    CreditsId = creditsEstimate.Id,
                    ProjectId = projectResponse.Id
                }
            ) ?? throw new Exception("Job start response was null.");
            Console.WriteLine($"Started job with ID: {startJobResponse.Id}");
            // Poll for the job status until it's completed using the GET /jobs/{id} endpoint
            // It answers with the same model as the StartJobsResponse so we can reuse the class
            Console.WriteLine("Monitoring job status until completion...");
            while (true)
            {
                Console.WriteLine("Waiting 5 minutes before polling job status...");
                await Task.Delay(5 * 60 * 1000); // 5 minutes
                var jobStatusResponse = await apiClient.GetFromJsonAsync<StartJobsResponse>(
                    $"jobs/{startJobResponse.Id}");
                if (jobStatusResponse == null)
                    throw new Exception("Failed to get job status.");
                Console.WriteLine($"Job Status: {jobStatusResponse.Status}");
                if (jobStatusResponse.Status == "completed")
                    break;
                Console.WriteLine("Job not completed yet, continuing to poll...");
            }
            // Let's retrieve all the results and save them to disk
            // Saving classifier results and extraction results separately
            Console.WriteLine("Querying classifier results for the uploaded documents...");
            var documentsResponse = await PostJsonAsync<DocumentsSearchRequest, DocumentsSearchResponse>(
                apiClient,
                "documents/search",
                new DocumentsSearchRequest
                {
                    Selectors = selectors
                }
            );
            await File.WriteAllTextAsync("classifier_results.json", JsonSerializer.Serialize(documentsResponse, new JsonSerializerOptions { WriteIndented = true }));
            // Extraction results
            Console.WriteLine("Querying extraction results scoped to the newly created project...");
            var extractionsResponse = await PostJsonAsync<DocumentsSearchRequest, ExtractionResponse>(
                apiClient,
                "documents/extraction_results/search",
                new DocumentsSearchRequest
                {
                    Selectors = new Selectors
                    {
                        Include = new Selector[]
                        {
                            new ProjectSelector { ProjectId = projectResponse.Id }
                        }
                    }
                }
            );
            await File.WriteAllTextAsync("extraction_results.json", JsonSerializer.Serialize(extractionsResponse, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Saved classifier_results.json and extraction_results.json to disk.");

            // Optional: dry-run a bulk delete to show which documents would be removed
            Console.WriteLine("Preparing dry-run bulk delete to preview cleanup...");
            var bulkDeleteRequest = new DocumentBulkDeleteRequest
            {
                Selectors = new Selectors
                {
                    Include = new Selector[]
                    {
                        new ProjectSelector { ProjectId = projectResponse.Id }
                    }
                },
                DryRun = true
            };

            var bulkDeleteResponse = await PostJsonAsync<DocumentBulkDeleteRequest, DocumentBulkDeleteResponse>(
                apiClient,
                "documents/bulk_delete",
                bulkDeleteRequest
            );
            Console.WriteLine($"Bulk delete dry-run matched {bulkDeleteResponse.Matched} documents (dryRun={bulkDeleteResponse.DryRun}).");
            // Now we can delete the documents
            Console.WriteLine("Executing actual bulk delete to remove processed documents...");
            bulkDeleteRequest.DryRun = false;
            bulkDeleteResponse = await PostJsonAsync<DocumentBulkDeleteRequest, DocumentBulkDeleteResponse>(
                apiClient,
                "documents/bulk_delete",
                bulkDeleteRequest
            );
            Console.WriteLine($"Bulk delete completed. Matched {bulkDeleteResponse.Matched} documents.");
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
        /// </summary>
        private static async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(
            HttpClient client,
            string url,
            TRequest body)
        {
            var response = await client.PostAsJsonAsync(url, body);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<TResponse>();
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

        private static void TryLoadDotEnv()
        {
            try
            {
                // Traverse upward so running from subdirectories still finds the repo-level .env
                Env.TraversePath().Load(".env");
            }
            catch (FileNotFoundException)
            {
                // Silently continue if no .env is present; environment variables may already be set.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: failed to load .env file. {ex.Message}");
            }
        }
    }
}
