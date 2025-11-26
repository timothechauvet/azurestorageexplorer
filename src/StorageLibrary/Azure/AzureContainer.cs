using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using StorageLibrary.Common;
using StorageLibrary.Interfaces;

namespace StorageLibrary.Azure
{
	internal class AzureContainer : StorageObject, IContainer
	{
		public AzureContainer(StorageFactoryConfig config)
		: base(config) { }

		public async Task<List<CloudBlobContainerWrapper>> ListContainersAsync()
		{
			BlobServiceClient blobServiceClient = new BlobServiceClient(ConnectionString);

			List<CloudBlobContainerWrapper> results = new List<CloudBlobContainerWrapper>();
			await foreach (var container in blobServiceClient.GetBlobContainersAsync())
			{
				results.Add(new CloudBlobContainerWrapper
				{
					Name = container.Name
				});
			}

			return results;
		}

		public async Task<List<BlobItemWrapper>> ListBlobsAsync(string containerName, string path)
		{
			Console.Error.WriteLine("--- START ListBlobsAsync ---");
			Console.Error.WriteLine($"Container Name: {containerName}, Path: {path}");

			BlobServiceClient blobServiceClient = new BlobServiceClient(ConnectionString);
			Console.Error.WriteLine($"BlobServiceClient created.");

			BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);
			Console.Error.WriteLine($"BlobContainerClient retrieved for '{containerName}'.");

			List<BlobItemWrapper> results = new List<BlobItemWrapper>();
			Console.Error.WriteLine($"Initialized results list.");

			await foreach (BlobHierarchyItem blobItem in container.GetBlobsByHierarchyAsync(options = GetBlobsByHierarchyOptions("/", path + "/"), cancellationToken = None))
			{
				Console.Error.WriteLine("--- Enumerated New Item ---");
				BlobItemWrapper wrapper = null;
				if (blobItem.IsBlob)
				{
					Console.Error.WriteLine($"Item is a Blob. Name: {blobItem.Blob.Name}");
					Console.Error.WriteLine($"Blob Content Length: {blobItem.Blob.Properties.ContentLength ?? 0}");
					BlobClient blobClient = container.GetBlobClient(blobItem.Blob.Name);

					string blobUrl = blobClient.Uri.AbsoluteUri;

					wrapper = new BlobItemWrapper(
						blobClient.Uri.AbsoluteUri,
						blobItem.Blob.Properties.ContentLength ?? 0,
						CloudProvider.Azure,
						IsAzurite,
						true
					);
					Console.Error.WriteLine("Wrapper created for Blob.");
				}
				else if (blobItem.IsPrefix)
				{
					Console.Error.WriteLine($"Item is a Prefix (Folder). Prefix Value: {blobItem.Prefix}");
					BlobClient prefixClient = container.GetBlobClient(blobItem.Prefix);

					Console.Error.WriteLine($"Generated Prefix URI: {prefixClient.Uri.AbsoluteUri}");
					wrapper = new BlobItemWrapper(
						prefixClient.Uri.AbsoluteUri,
						0,
						CloudProvider.Azure,
						IsAzurite,
						false
					);
					Console.Error.WriteLine("Wrapper created for Prefix.");
				}

				if (wrapper != null && !results.Contains(wrapper))
					Console.Error.WriteLine($"Wrapper is NOT NULL. URL: {wrapper.Url}");
					results.Add(wrapper);
			}

			return results;
		}

		public async Task DeleteAsync(string containerName)
		{
			BlobServiceClient blobServiceClient = new BlobServiceClient(ConnectionString);
			BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);

			await container.DeleteAsync();
		}

		public async Task CreateAsync(string containerName, bool publicAccess)
		{
			BlobServiceClient blobServiceClient = new BlobServiceClient(ConnectionString);
			BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);

			PublicAccessType accessType = publicAccess ? PublicAccessType.BlobContainer : PublicAccessType.None;
			await container.CreateAsync(accessType);
		}

		public async Task DeleteBlobAsync(string containerName, string blobName)
		{

			BlobServiceClient blobServiceClient = new BlobServiceClient(ConnectionString);
			BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);

			BlobClient blob = container.GetBlobClient(blobName);

			await blob.DeleteAsync();
		}

		public async Task CreateBlobAsync(string containerName, string blobName, Stream fileContent)
		{
			BlobServiceClient blobServiceClient = new BlobServiceClient(ConnectionString);
			BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);

			await container.UploadBlobAsync(blobName, fileContent);
		}

		public async Task<string> GetBlobAsync(string containerName, string blobName)
		{
			BlobServiceClient blobServiceClient = new BlobServiceClient(ConnectionString);
			BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);

			BlobClient blob = container.GetBlobClient(blobName);

			string tmpPath = Util.File.GetTempFileName();
			await blob.DownloadToAsync(tmpPath);

			return tmpPath;
		}
	}
}
