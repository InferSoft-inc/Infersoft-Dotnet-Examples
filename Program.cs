using DotNetEnv;
using Examples.Model;
using System.Net.Http.Json;
using System.Text.Json;

namespace Examples
{
    internal class Program
    {
        private const string Auth0BaseUrl = "https://dev-noabnisxxguu0jp0.us.auth0.com";
        private const string ApiBaseUrl = "https://api.infersoft.com/api/";

        // Serialization options: snake_case naming, ignore nulls
        private static readonly JsonSerializerOptions SerializeOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        private static readonly JsonSerializerOptions DeserializeOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
        };

        private static readonly JsonSerializerOptions IndentedSerializeOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

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
            apiClient.DefaultRequestHeaders.Add("Accept", "application/json");
            apiClient.DefaultRequestHeaders.Add("User-Agent", "infersoft-dotnet-examples/1.0");

            // ── Upload Phase ──
            // Upload all 3 files in one batch with a single tag.
            // We use this tag with TagSelector to target documents for classification and extraction.

            var projectName = $"C# SDK Project {DateTime.UtcNow:yyyyMMdd-HHmmss}";
            var pdfFiles = new List<string> { "docs/doc_1.pdf", "docs/doc_2.pdf", "docs/doc_3.pdf" };

            // Clean up any leftover documents from previous runs with the same file names
            var allFileNames = pdfFiles.Select(Path.GetFileName).ToList();
            await DeleteExistingDocumentsByName(apiClient, allFileNames!);

            // ── Upload all files with a single tag ──
            Console.WriteLine($"Uploading {pdfFiles.Count} files with tag 'example-run' to project '{projectName}'...");
            var uploadRequest = new UploadRequest
            {
                Files = pdfFiles.Select(path => new UploadFile
                {
                    ContentType = "application/pdf",
                    FileName = Path.GetFileName(path),
                    Size = new FileInfo(path).Length
                }).ToList(),
                ProjectName = projectName,
                TagIds = new List<int>(),
                TagNames = new List<string> { "example-run" }
            };

            var uploadResponse = await PostJsonAsync<UploadRequest, UploadResponse>(apiClient, "uploads", uploadRequest)
                ?? throw new Exception("Upload response was null.");

            await UploadFilesToPresignedUrls(pdfFiles, uploadResponse.Items);
            Console.WriteLine("All files uploaded successfully.");

            var tagId = uploadResponse.Tags.First(t => t.Name == "example-run").Id;
            var projectId = uploadResponse.Project?.Id
                ?? throw new Exception("Upload response did not contain project information.");
            Console.WriteLine($"Tag ID: {tagId}, Project ID: {projectId}");

            // ── Selector ──
            // Single TagSelector to target all uploaded documents
            var tagSelectors = new Selectors
            {
                Include = new Selector[]
                {
                    new TagSelector { Tags = new[] { tagId } }
                }
            };

            // Step 1: Classification Job
            Console.WriteLine("Step 1: Requesting credit estimate for classification...");
            var classifyBudgetRequest = new EstimateCreditsRequest
            {
                Prompts = Array.Empty<int>(),
                Selectors = tagSelectors,
                Steps = new[] { "classifier" },
                Synchronous = true // We use true here, setting it to false is half price but takes up to 24h
            };

            var classifyCreditsEstimate = await PostJsonAsync<EstimateCreditsRequest, EstimateCreditsResponse>(
                apiClient,
                "jobs/credits/estimate",
                classifyBudgetRequest
            ) ?? throw new Exception("Classification budget response was null.");

            Console.WriteLine($"Estimated credits for classification: {classifyCreditsEstimate.TotalCredits} over {classifyCreditsEstimate.PageCount} pages. ID: {classifyCreditsEstimate.Id}");

            // Start classification job
            Console.WriteLine("Starting classification job...");
            var classifyJobResponse = await PostJsonAsync<StartJobsRequest, StartJobsResponse>(
                apiClient,
                "jobs/start",
                new StartJobsRequest
                {
                    CreditsId = classifyCreditsEstimate.Id
                }
            ) ?? throw new Exception("Classification job start response was null.");
            Console.WriteLine($"Started classification job with ID: {classifyJobResponse.Id}");

            // Poll for classification job completion
            Console.WriteLine("Monitoring classification job status until completion...");
            while (true)
            {
                Console.WriteLine("Waiting 2 minutes before polling job status...");
                await Task.Delay(2 * 60 * 1000); // 2 minutes
                var jobStatusResponse = await apiClient.GetFromJsonAsync<StartJobsResponse>(
                    $"jobs/{classifyJobResponse.Id}");
                if (jobStatusResponse == null)
                    throw new Exception("Failed to get classification job status.");
                Console.WriteLine($"Classification Job Status: {jobStatusResponse.Status}");
                if (jobStatusResponse.Status == "completed")
                    break;
                if (jobStatusResponse.Status == "failed")
                    throw new Exception("Classification job failed.");
                Console.WriteLine("Classification job not completed yet, continuing to poll...");
            }

            // Step 2: Extraction Job
            // First, query available prompts
            Console.WriteLine("Querying available prompts for extraction...");
            var promptsResponse = await PostJsonAsync<PromptQueryRequest, PromptQueryResponse>(
                apiClient,
                "prompts/search",
                new PromptQueryRequest { PageSize = 10 }
            );

            if (promptsResponse == null || promptsResponse.Items.Count == 0)
            {
                Console.WriteLine("No prompts available. Skipping extraction step.");
                return;
            }

            var availablePromptIds = promptsResponse.Items.Select(p => p.Id).Take(4).ToArray();
            Console.WriteLine($"Using prompts: {string.Join(", ", availablePromptIds)}");

            // ── Step 2: Extraction (documents selected via TagSelector) ──
            Console.WriteLine($"Step 2: Extracting documents with tag 'example-run' (tag ID {tagId})...");
            var extractBudgetRequest = new EstimateCreditsRequest
            {
                Prompts = availablePromptIds,
                Selectors = tagSelectors,
                Steps = new[] { "extractor" },
                Synchronous = true
            };

            var extractCreditsEstimate = await PostJsonAsync<EstimateCreditsRequest, EstimateCreditsResponse>(
                apiClient,
                "jobs/credits/estimate",
                extractBudgetRequest
            ) ?? throw new Exception("Extraction budget response was null.");

            Console.WriteLine($"Estimated credits for extraction: {extractCreditsEstimate.TotalCredits} over {extractCreditsEstimate.PageCount} pages. ID: {extractCreditsEstimate.Id}");

            // Start extraction job
            Console.WriteLine("Starting extraction job...");
            var extractJobResponse = await PostJsonAsync<StartJobsRequest, StartJobsResponse>(
                apiClient,
                "jobs/start",
                new StartJobsRequest
                {
                    CreditsId = extractCreditsEstimate.Id,
                    ProjectId = projectId
                }
            ) ?? throw new Exception("Extraction job start response was null.");
            Console.WriteLine($"Started extraction job with ID: {extractJobResponse.Id}");

            // Poll for extraction job completion
            Console.WriteLine("Monitoring extraction job status until completion...");
            while (true)
            {
                Console.WriteLine("Waiting 2 minutes before polling job status...");
                await Task.Delay(2 * 60 * 1000); // 2 minutes
                var jobStatusResponse = await apiClient.GetFromJsonAsync<StartJobsResponse>(
                    $"jobs/{extractJobResponse.Id}");
                if (jobStatusResponse == null)
                    throw new Exception("Failed to get extraction job status.");
                Console.WriteLine($"Extraction Job Status: {jobStatusResponse.Status}");
                if (jobStatusResponse.Status == "completed")
                    break;
                if (jobStatusResponse.Status == "failed")
                    throw new Exception("Extraction job failed.");
                Console.WriteLine("Extraction job not completed yet, continuing to poll...");
            }
            // Let's retrieve all the results and save them to disk
            // Classification results: all 3 documents (project-wide)
            Console.WriteLine("Querying classifier results for ALL uploaded documents...");
            var documentsResponse = await PostJsonAsync<DocumentsSearchRequest, DocumentsSearchResponse>(
                apiClient,
                "documents/search",
                new DocumentsSearchRequest
                {
                    Selectors = tagSelectors
                }
            );
            await File.WriteAllTextAsync("classifier_results.json", JsonSerializer.Serialize(documentsResponse, IndentedSerializeOptions));

            // Extraction results — this endpoint requires a projectSelector in include
            Console.WriteLine("Querying extraction results scoped to 'example-run' tag...");
            var extractionSelectors = new Selectors
            {
                Include = new Selector[]
                {
                    new TagSelector { Tags = new[] { tagId } },
                    new ProjectSelector { ProjectId = projectId }
                }
            };
            var extractionsResponse = await PostJsonAsync<DocumentsSearchRequest, ExtractionResponse>(
                apiClient,
                "documents/extraction_results/search",
                new DocumentsSearchRequest
                {
                    Selectors = extractionSelectors
                }
            );
            await File.WriteAllTextAsync("extraction_results.json", JsonSerializer.Serialize(extractionsResponse, IndentedSerializeOptions));
            Console.WriteLine("Saved classifier_results.json and extraction_results.json to disk.");

            // Optional: dry-run a bulk delete to show which documents would be removed
            Console.WriteLine("Preparing dry-run bulk delete to preview cleanup...");
            var bulkDeleteRequest = new DocumentBulkDeleteRequest
            {
                Selectors = tagSelectors,
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
        /// Uses StringContent to avoid chunked transfer encoding which CloudFront blocks.
        /// </summary>
        private static async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(
            HttpClient client,
            string url,
            TRequest body)
        {
            var jsonContent = JsonSerializer.Serialize(body, SerializeOptions);
            using var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            var request = response.RequestMessage;

            if (request?.Content != null)
            {
                Console.WriteLine($"Request Content: {await request.Content.ReadAsStringAsync()}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Request to '{request?.RequestUri}' failed with status {(int)response.StatusCode} ({response.StatusCode}). Body: {responseBody}");
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                throw new HttpRequestException(
                    $"Request to '{request?.RequestUri}' returned empty body with status {(int)response.StatusCode}. Headers: {response.Headers}");
            }

            return JsonSerializer.Deserialize<TResponse>(responseBody, DeserializeOptions);
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

        /// <summary>
        /// Uploads each local file to its corresponding presigned URL.
        /// </summary>
        private static async Task UploadFilesToPresignedUrls(
            List<string> localPaths,
            List<UploadItem> uploadItems)
        {
            if (uploadItems.Count != localPaths.Count)
                throw new Exception("Upload response item count does not match the number of files.");

            for (int i = 0; i < localPaths.Count; i++)
            {
                var path = localPaths[i];
                var item = uploadItems[i];
                if (item.Error != null)
                    throw new Exception($"Upload failed for {item.ClientFileName}: [{item.Error.Code}] {item.Error.Message}");
                if (string.IsNullOrEmpty(item.PutUrl))
                    throw new Exception($"No presigned URL returned for {item.ClientFileName}.");
                Console.WriteLine($"  Uploading {path} → {item.PutUrl}");
                await UploadFileAsync(item.PutUrl, path, item.RequiredHeaders);
            }
        }

        /// <summary>
        /// Searches for documents matching the given file names and deletes them.
        /// Prevents "duplicate file" errors when re-running the example.
        /// </summary>
        private static async Task DeleteExistingDocumentsByName(
            HttpClient apiClient,
            List<string> fileNames)
        {
            foreach (var name in fileNames)
            {
                var searchResponse = await PostJsonAsync<DocumentsSearchRequest, DocumentsSearchResponse>(
                    apiClient,
                    "documents/search",
                    new DocumentsSearchRequest
                    {
                        Selectors = new Selectors
                        {
                            Include = new Selector[] { new NameSelector { Name = name } }
                        }
                    }
                );

                if (searchResponse?.Items == null || searchResponse.Items.Count == 0)
                    continue;

                foreach (var doc in searchResponse.Items)
                {
                    Console.WriteLine($"Cleaning up existing document: {doc.Name} (ID {doc.Id})");
                    var resp = await apiClient.DeleteAsync($"documents/{doc.Id}");
                    resp.EnsureSuccessStatusCode();
                }
            }
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
