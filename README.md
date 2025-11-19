# Infersoft .NET SDK Example

This example demonstrates an end-to-end document processing workflow using the Infersoft API.

## What it does

1.  **Authenticates** with Auth0.
2.  **Uploads** 3 sample PDF documents (`docs/`).
3.  **Creates a Project** and assigns the uploaded documents.
4.  **Classifies** the documents (polls for completion).
5.  **Extracts** data using available prompts (polls for completion).
6.  **Saves Results** to `classifier_results.json` and `extraction_results.json`.
7.  **Cleans up** by deleting the processed documents (dry-run first, then actual delete).

## Prerequisites

*   .NET 10.0 SDK
*   Infersoft API Credentials (`Client ID` and `Client Secret`)

## Setup

1.  Create a `.env` file in the root directory:

```env
AUTH0_CLIENT_ID=your_client_id_here
AUTH0_CLIENT_SECRET=your_client_secret_here
```

2.  Restore dependencies:

```bash
dotnet restore
```

## Run

```bash
dotnet run
```

## Output

*   `classifier_results.json`: Document classification details.
*   `extraction_results.json`: Extracted data based on prompts.
