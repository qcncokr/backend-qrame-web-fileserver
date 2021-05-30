using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;

using Qrame.CoreFX.ExtensionMethod;
using Qrame.CoreFX.Helper;
using Qrame.Web.FileServer.Entities;
using Qrame.Web.FileServer.Extensions;
using Qrame.Web.FileServer.Message;

using Serilog;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Qrame.Web.FileServer.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[EnableCors()]
	public class FileManagerController : ControllerBase
	{
		private ILogger logger { get; }

		private ILiteDBClient liteDBClient { get; }

		private IConfiguration configuration { get; }

		public FileManagerController(ILogger logger, ILiteDBClient liteDBClient, IConfiguration configuration)
		{
			this.logger = logger;
			this.liteDBClient = liteDBClient;
			this.configuration = configuration;
		}

		// http://localhost:7004/api/FileManager/GetToken?remoteIP=localhost
		[HttpGet("GetToken")]
		public string GetToken(string remoteIP)
		{
			string result = "";
			if (StaticConfig.TokenGenerateIPCheck == true)
			{
				if (GetClientIP() == remoteIP)
				{
					result = ClientSessionManager.GetToken(remoteIP);
				}
			}
			else
			{
				result = ClientSessionManager.GetToken(remoteIP);
			}

			return result;
		}

		// http://localhost:7004/api/FileManager/RequestIP
		[HttpGet("RequestIP")]
		public string GetClientIP()
		{
			return HttpContext.Features.Get<IHttpConnectionFeature>()?.RemoteIpAddress?.ToString();
		}

		// http://localhost:7004/api/FileManager/ActionHandler
		[HttpGet("ActionHandler")]
		public async Task<ActionResult> ActionHandler()
		{
			ActionResult result = NotFound();

			JsonContentResult jsonContentResult = new JsonContentResult();
			jsonContentResult.Result = false;

			string action = Request.Query["action"].ToString();

			switch (action.ToUpper())
			{
				case "GETITEM":
					result = await GetItem(result, jsonContentResult);
					break;
				case "GETITEMS":
					result = await GetItems(result, jsonContentResult);
					break;
				case "UPDATEDEPENDENCYID":
					result = await UpdateDependencyID(result, jsonContentResult);
					break;
				case "UPDATEFILENAME":
					result = await UpdateFileName(result, jsonContentResult);
					break;
			}

			Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
			Response.Headers["Qrame_ModelType"] = "JsonContentResult";
			
			Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jsonContentResult)));
			return result;
		}

		private async Task<ActionResult> UpdateDependencyID(ActionResult result, JsonContentResult jsonContentResult)
		{
			string repositoryID = Request.Query["repositoryID"].ToString();
			string sourceDependencyID = Request.Query["sourceDependencyID"].ToString();
			string targetDependencyID = Request.Query["targetDependencyID"].ToString();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(sourceDependencyID) == true || string.IsNullOrEmpty(targetDependencyID) == true)
			{
				string message = $"UPDATEDEPENDENCYID 요청 정보가 유효하지 않습니다, repositoryID: {repositoryID}, sourceDependencyID: {sourceDependencyID}, targetDependencyID: {targetDependencyID}";
				jsonContentResult.Message = message;

				logger.Information("[{LogCategory}] " + message, "FileManagerController/UpdateDependencyID");
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				jsonContentResult.Message = "RepositoryID 요청 정보 확인 필요";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/UpdateDependencyID");
				return result;
			}

			List<RepositoryItems> items = null;
			if (StaticConfig.IsLocalDB == true)
			{
				items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == sourceDependencyID).ToList();
			}
			else
			{
				items = await businessApiClient.GetRepositoryItems(repositoryID, sourceDependencyID);
			}

			bool isDataUpsert = false;
			if (items != null && items.Count > 0)
			{
				for (int i = 0; i < items.Count; i++)
				{
					RepositoryItems item = items[i];

					if (StaticConfig.IsLocalDB == true)
					{
						isDataUpsert = liteDBClient.Update<RepositoryItems>(item);
					}
					else
					{
						isDataUpsert = await businessApiClient.UpdateDependencyID(item, targetDependencyID);
					}

					if (isDataUpsert == false)
					{
						jsonContentResult.Result = false;
						jsonContentResult.Message = "UpdateDependencyID 데이터 거래 오류";
						logger.Warning("[{LogCategory}] 데이터 거래 오류 " + JsonConvert.SerializeObject(item), "FileManagerController/UpdateDependencyID");
						result = Content(JsonConvert.SerializeObject(jsonContentResult), "application/json");
						return result;
					}
				}

				jsonContentResult.Result = true;
			}
			else
			{
				jsonContentResult.Message = $"DependencyID: '{sourceDependencyID}' 파일 요청 정보 확인 필요";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/UpdateDependencyID");
			}

			result = Content(JsonConvert.SerializeObject(jsonContentResult), "application/json");

			return result;
		}

		private async Task<ActionResult> UpdateFileName(ActionResult result, JsonContentResult jsonContentResult)
		{
			string repositoryID = Request.Query["repositoryID"].ToString();
			string itemID = Request.Query["itemID"].ToString();
			string changeFileName = Request.Query["fileName"].ToString();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true || string.IsNullOrEmpty(changeFileName) == true)
			{
				string message = $"UPDATEFILENAME 요청 정보가 유효하지 않습니다, repositoryID: {repositoryID}, itemID: {itemID}, fileName: {changeFileName}";
				jsonContentResult.Message = message;

				logger.Information("[{LogCategory}] " + message, "FileManagerController/UpdateFileName");
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				jsonContentResult.Message = "RepositoryID 요청 정보 확인 필요";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/UpdateFileName");
				return result;
			}

			RepositoryItems item = null;
			if (StaticConfig.IsLocalDB == true)
			{
				item = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.ItemID == itemID).FirstOrDefault();
			}
			else
			{
				item = await businessApiClient.GetRepositoryItem(repositoryID, itemID);
			}

			bool isDataUpsert = false;
			if (item != null)
			{
				string customPath1 = item.CustomPath1;
				string customPath2 = item.CustomPath2;
				string customPath3 = item.CustomPath3;

				BlobContainerClient container = null;
				bool hasContainer = false;
				if (repository.StorageType == "AzureBlob")
				{
					container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
					hasContainer = await container.ExistsAsync();
				}

				RepositoryManager repositoryManager = new RepositoryManager();
				repositoryManager.PersistenceDirectoryPath = repositoryManager.GetPhysicalPath(repository, customPath1, customPath2, customPath3);
				string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, customPath1, customPath2, customPath3);
				string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
				relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";
				string policyPath = repositoryManager.GetPolicyPath(repository);

				string newItemID = repository.IsFileNameEncrypt.ParseBool() == true ? Guid.NewGuid().ToString().Replace("-", string.Empty).ToUpper() : changeFileName;
				bool isExistFile = false;
				// 파일명 변경
				switch (repository.StorageType)
				{
					case "AzureBlob":
						if (hasContainer == true)
						{
							string fileName;
							if (repository.IsFileNameEncrypt.ParseBool() == true)
							{
								fileName = item.ItemID;
							}
							else
							{
								fileName = item.FileName;
							}
							string blobID = relativeDirectoryUrlPath + fileName;

							BlobClient blob = container.GetBlobClient(blobID);
							isExistFile = await blob.ExistsAsync();
							if (isExistFile == true)
							{
								BlobDownloadInfo blobDownloadInfo = await blob.DownloadAsync();
								BlobProperties properties = await blob.GetPropertiesAsync();
								BlobHttpHeaders headers = new BlobHttpHeaders
								{
									ContentType = properties.ContentType
								};

								string newBlobID = relativeDirectoryUrlPath + newItemID;
								BlobClient newBlob = container.GetBlobClient(newBlobID);
								await newBlob.UploadAsync(blobDownloadInfo.Content, headers);
								await container.DeleteBlobIfExistsAsync(blobID);
							}
						}
						break;
					default:
						isExistFile = System.IO.File.Exists(repositoryManager.GetSavePath(item.FileName));
						if (isExistFile == true)
						{
							repositoryManager.Move(item.FileName, repositoryManager.GetSavePath(changeFileName));
						}
						break;
				}

				if (isExistFile == false)
				{
					jsonContentResult.Message = $"파일 없음, 파일 요청 정보 확인 필요. repositoryID: {repositoryID}, itemID: {itemID}, fileName: {changeFileName}";
					logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/UpdateFileName");
				}

				string backupItemID = item.ItemID;
				item.ItemID = newItemID;
				item.RelativePath = item.RelativePath.Replace(item.FileName, changeFileName);
				item.AbsolutePath = item.AbsolutePath.Replace(item.FileName, changeFileName);
				item.FileName = changeFileName;

				if (StaticConfig.IsLocalDB == true)
				{
					isDataUpsert = liteDBClient.Upsert<RepositoryItems>(item);

					if (isDataUpsert == true)
					{
						liteDBClient.Delete<RepositoryItems>(p => p.ItemID == backupItemID);
					}
				}
				else
				{
					isDataUpsert = await businessApiClient.UpdateFileName(item, backupItemID);
				}

				if (isDataUpsert == false)
				{
					jsonContentResult.Result = false;
					jsonContentResult.Message = "UpdateDependencyID 데이터 거래 오류";
					logger.Warning("[{LogCategory}] 데이터 거래 오류 " + JsonConvert.SerializeObject(item), "FileManagerController/UpdateDependencyID");
					result = Content(JsonConvert.SerializeObject(jsonContentResult), "application/json");
					return result;
				}

				jsonContentResult.Result = true;
			}
			else
			{
				jsonContentResult.Message = $"데이터 없음, 파일 요청 정보 확인 필요. repositoryID: {repositoryID}, itemID: {itemID}, fileName: {changeFileName}";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/UpdateFileName");
			}

			result = Content(JsonConvert.SerializeObject(jsonContentResult), "application/json");

			return result;
		}

		private async Task<ActionResult> GetItem(ActionResult result, JsonContentResult jsonContentResult)
		{
			string repositoryID = Request.Query["repositoryID"].ToString();
			string itemID = Request.Query["itemID"].ToString();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				jsonContentResult.Message = $"GETITEM 요청 정보가 유효하지 않습니다, repositoryID: {repositoryID}, dependencyID: {itemID}";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItem");
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				jsonContentResult.Message = $"RepositoryID: '{repositoryID}' 파일 요청 정보 확인 필요";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItem");
				return result;
			}

			RepositoryItems item = null;
			if (StaticConfig.IsLocalDB == true)
			{
				item = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.ItemID == itemID).FirstOrDefault();
			}
			else
			{
				item = await businessApiClient.GetRepositoryItem(repositoryID, itemID);
			}

			if (item != null)
			{
				var entity = new
				{
					ItemID = item.ItemID,
					RepositoryID = item.RepositoryID,
					DependencyID = item.DependencyID,
					FileName = item.FileName,
					Sequence = item.OrderBy,
					AbsolutePath = item.AbsolutePath,
					RelativePath = item.RelativePath,
					Extension = item.Extension,
					Size = item.FileLength,
					MimeType = item.MimeType,
					CustomPath1 = item.CustomPath1,
					CustomPath2 = item.CustomPath2,
					CustomPath3 = item.CustomPath3,
					PolicyPath = item.PolicyPath,
					MD5 = item.MD5
				};

				jsonContentResult.Result = true;
				result = Content(JsonConvert.SerializeObject(entity), "application/json");
			}
			else
			{
				jsonContentResult.Message = $"ItemID: '{itemID}' 파일 요청 정보 확인 필요";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItem");
				result = Content("{}", "application/json");
			}

			return result;
		}

		private async Task<ActionResult> GetItems(ActionResult result, JsonContentResult jsonContentResult)
		{
			string repositoryID = Request.Query["repositoryID"].ToString();
			string dependencyID = Request.Query["dependencyID"].ToString();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(dependencyID) == true)
			{
				jsonContentResult.Message = $"GETITEMS 요청 정보가 유효하지 않습니다, repositoryID: {repositoryID}, dependencyID: {dependencyID}";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItems");
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				jsonContentResult.Message = $"RepositoryID: '{repositoryID}' 파일 요청 정보 확인 필요";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItems");
				return result;
			}

			List<RepositoryItems> items = null;
			if (StaticConfig.IsLocalDB == true)
			{
				items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID).ToList();
			}
			else
			{
				items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID);
			}

			List<dynamic> entitys = new List<dynamic>();
			if (items != null)
			{
				foreach (RepositoryItems item in items)
				{
					entitys.Add(new
					{
						ItemID = item.ItemID,
						RepositoryID = item.RepositoryID,
						DependencyID = item.DependencyID,
						FileName = item.FileName,
						Sequence = item.OrderBy,
						AbsolutePath = item.AbsolutePath,
						RelativePath = item.RelativePath,
						Extension = item.Extension,
						Size = item.FileLength,
						MimeType = item.MimeType,
						CustomPath1 = item.CustomPath1,
						CustomPath2 = item.CustomPath2,
						CustomPath3 = item.CustomPath3,
						PolicyPath = item.PolicyPath,
						MD5 = item.MD5
					});
				}
			}
			else
			{
				jsonContentResult.Message = $"DependencyID: '{dependencyID}' 파일 요청 정보 확인 필요";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItems");
				result = Content("{}", "application/json");
			}

			jsonContentResult.Result = true;
			result = Content(JsonConvert.SerializeObject(entitys), "application/json");

			return result;
		}

		// http://localhost:7004/api/FileManager/RepositoryRefresh
		[HttpGet("RepositoryRefresh")]
		public async Task<ContentResult> RepositoryRefresh()
		{
			string result = "true";

			try
			{
				if (StaticConfig.IsLocalDB == true)
				{
					LiteDBClient liteDBClient = new LiteDBClient(logger, configuration);
					liteDBClient.Delete<Repository>();

					string repository = System.IO.File.ReadAllText(Path.Combine(StaticConfig.ContentRootPath, "repository.json"));
					StaticConfig.FileRepositorys = JsonConvert.DeserializeObject<List<Repository>>(repository);

					liteDBClient.Inserts(StaticConfig.FileRepositorys);
				}
				else
				{
					BusinessApiClient businessApiClient = new BusinessApiClient(logger);
					StaticConfig.FileRepositorys = await businessApiClient.GetRepositorys(StaticConfig.RepositoryList);
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "FileManagerController/RepositoryRefresh");
				result = "false";
			}

			return Content(result, "application/json", Encoding.UTF8);
		}

		// http://localhost:7004/api/FileManager/GetRepository
		[HttpGet("GetRepository")]
		public ContentResult GetRepository(string repositoryID)
		{
			string result = "{}";

			if (string.IsNullOrEmpty(repositoryID) == false)
			{
				BusinessApiClient businessApiClient = new BusinessApiClient(logger);
				Repository repository = businessApiClient.GetRepository(repositoryID);

				if (repository != null)
				{
					var entity = new
					{
						RepositoryID = repository.RepositoryID,
						RepositoryName = repository.RepositoryName,
						StorageType = repository.StorageType,
						IsMultiUpload = repository.IsMultiUpload,
						IsAutoPath = repository.IsAutoPath,
						PolicyPathID = repository.PolicyPathID,
						UploadType = repository.UploadType,
						UploadExtensions = repository.UploadExtensions,
						UploadCount = repository.UploadCount,
						UploadSizeLimit = repository.UploadSizeLimit
					};
					result = JsonConvert.SerializeObject(entity);
				}
			}

			return Content(result, "application/json", Encoding.UTF8);
		}

		// http://localhost:7004/api/FileManager/UploadFile
		[HttpPost("UploadFile")]
		public async Task<ContentResult> UploadFile([FromForm] IFormFile file)
		{
			FileUploadResult result = new FileUploadResult();
			result.Result = false;
			string repositoryID = Request.Query["RepositoryID"];
			string dependencyID = Request.Query["DependencyID"];

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(dependencyID) == true)
			{
				result.Message = "RepositoryID 또는 DependencyID 필수 요청 정보 필요";
				return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				result.Message = "RepositoryID 요청 정보 확인 필요";
				return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
			}

			int sequence = string.IsNullOrEmpty(Request.Query["Sequence"]) == true ? 1 : Request.Query["Sequence"].ToString().GetInt();
			string saveFileName = string.IsNullOrEmpty(Request.Query["FileName"]) == true ? "" : Request.Query["FileName"].ToString();
			string itemSummary = string.IsNullOrEmpty(Request.Query["ItemSummary"]) == true ? "" : Request.Query["ItemSummary"].ToString();
			string customPath1 = string.IsNullOrEmpty(Request.Query["CustomPath1"]) == true ? "" : Request.Query["CustomPath1"].ToString();
			string customPath2 = string.IsNullOrEmpty(Request.Query["CustomPath2"]) == true ? "" : Request.Query["CustomPath2"].ToString();
			string customPath3 = string.IsNullOrEmpty(Request.Query["CustomPath3"]) == true ? "" : Request.Query["CustomPath3"].ToString();
			string userID = string.IsNullOrEmpty(Request.Query["UserID"]) == true ? "system" : Request.Query["UserID"].ToString();

			RepositoryItems repositoryItem = null;

			if (Request.HasFormContentType == true)
			{
				if (file == null)
				{
					result.Message = "업로드 파일 정보 없음";
				}
				else
				{
					try
					{
						#region Form 업로드

						if (repository.UploadSizeLimit < ToFileLength(file.Length))
						{
							result.Message = repository.UploadSizeLimit.ToCurrencyString() + " 바이트 이상 업로드 할 수 없습니다";
							return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
						}

						RepositoryManager repositoryManager = new RepositoryManager();
						repositoryManager.PersistenceDirectoryPath = repositoryManager.GetPhysicalPath(repository, customPath1, customPath2, customPath3);
						string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, customPath1, customPath2, customPath3);
						string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
						relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";

						if (repository.IsMultiUpload.ParseBool() == true) {
							List<RepositoryItems> items = null;
							if (StaticConfig.IsLocalDB == true)
							{
								items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID).ToList();
							}
							else
							{
								items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID);
							}

							if (items != null && items.Count() > 0)
							{
								if (items.Count >= repository.UploadCount)
								{
									result.Message = repository.UploadCount.ToCurrencyString() + " 파일 갯수 이상 업로드 할 수 없습니다";
									return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
								}
							}

							result.RemainingCount = repository.UploadCount - (items.Count + 1);
						}
						else
						{
							List<RepositoryItems> items = null;
							if (StaticConfig.IsLocalDB == true)
							{
								items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID).ToList();
							}
							else
							{
								items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID);
							}

							if (items != null && items.Count() > 0)
							{
								BlobContainerClient container = null;
								bool hasContainer = false;
								if (repository.StorageType == "AzureBlob")
								{
									container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
									hasContainer = await container.ExistsAsync();
								}

								foreach (RepositoryItems item in items)
								{
									string deleteFileName;
									if (repository.IsFileNameEncrypt.ParseBool() == true)
									{
										deleteFileName = item.ItemID;
									}
									else
									{
										deleteFileName = item.FileName;
									}

									switch (repository.StorageType)
									{
										case "AzureBlob":
											if (hasContainer == true)
											{
												string blobID = relativeDirectoryUrlPath + deleteFileName;
												await container.DeleteBlobIfExistsAsync(blobID);
											}
											break;
										default:
											string filePath = relativeDirectoryPath + deleteFileName;
											repositoryManager.Delete(filePath);
											break;
									}

									if (StaticConfig.IsLocalDB == true)
									{
										liteDBClient.Delete<RepositoryItems>(p => p.ItemID == item.ItemID);
									}
									else
									{
										await businessApiClient.DeleteRepositoryItem(repositoryID, item.ItemID);
									}
								}
							}
						}

						string absolutePath = "";
						string relativePath = "";
						string policyPath = repositoryManager.GetPolicyPath(repository);
						string fileName = string.IsNullOrEmpty(saveFileName) == true ? file.FileName : saveFileName;
						string extension = Path.GetExtension(fileName);
						if (string.IsNullOrEmpty(extension) == true)
						{
							extension = Path.GetExtension(file.FileName);
						}

						repositoryItem = new RepositoryItems();
						repositoryItem.ItemID = repository.IsFileNameEncrypt.ParseBool() == true ? Guid.NewGuid().ToString().Replace("-", string.Empty).ToUpper() : fileName;
						repositoryItem.OrderBy = sequence;
						repositoryItem.ItemSummary = itemSummary;
						repositoryItem.FileName = fileName;
						repositoryItem.Extension = extension;
						repositoryItem.MimeType = GetMimeType(file.FileName);
						repositoryItem.FileLength = file.Length;
						repositoryItem.RepositoryID = repositoryID;
						repositoryItem.DependencyID = dependencyID;
						repositoryItem.CustomPath1 = customPath1;
						repositoryItem.CustomPath2 = customPath2;
						repositoryItem.CustomPath3 = customPath3;
						repositoryItem.PolicyPath = policyPath;
						repositoryItem.CreateUserID = userID;
						repositoryItem.CreateDateTime = DateTime.Now;

						switch (repository.StorageType)
						{
							case "AzureBlob":
								BlobContainerClient container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
								await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
								string blobID = relativeDirectoryUrlPath + repositoryItem.ItemID;
								BlobClient blob = container.GetBlobClient(blobID);

								if (repository.IsFileNameEncrypt.ParseBool() == false && repository.IsFileOverWrite.ParseBool() == false) {
									blobID = await repositoryManager.GetDuplicateCheckUniqueFileName(container, blobID);
									repositoryItem.ItemID = blobID;
									repositoryItem.FileName = blobID;
								}

								Stream openReadStream = file.OpenReadStream();

								BlobHttpHeaders headers = new BlobHttpHeaders
								{
									ContentType = repositoryItem.MimeType
								};

								await blob.UploadAsync(openReadStream, headers);

								BlobProperties properties = await blob.GetPropertiesAsync();

								repositoryItem.PhysicalPath = "";
								repositoryItem.MD5 = GetStreamMD5Hash(openReadStream);
								repositoryItem.CreationTime = properties.CreatedOn.LocalDateTime;
								repositoryItem.LastWriteTime = properties.LastModified.LocalDateTime;

								if (repository.IsVirtualPath.ParseBool() == true)
								{
									relativePath = relativeDirectoryUrlPath + repositoryItem.ItemID;
									if (string.IsNullOrEmpty(repository.AzureBlobItemUrl) == true)
									{
										absolutePath = $"//{repository.RepositoryID}.blob.core.windows.net/{repository.AzureBlobContainerID.ToLower()}/";
										absolutePath = absolutePath + relativePath;
									}
									else
									{
										absolutePath = repository.AzureBlobItemUrl
											.Replace("[CONTAINERID]", repository.AzureBlobContainerID.ToLower())
											.Replace("[BLOBID]", relativePath);
									}
								}
								else
								{
									relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}";
									relativePath = Request.Path.Value.Replace("/UploadFile", "") + relativePath;
									absolutePath = "//" + Request.Host.Value + relativePath;
								}

								repositoryItem.RelativePath = relativePath;
								repositoryItem.AbsolutePath = absolutePath;
								break;
							default:
								string itemPhysicalPath = repositoryManager.GetSavePath(repositoryItem.ItemID);

								if (repository.IsFileNameEncrypt.ParseBool() == false && repository.IsFileOverWrite.ParseBool() == false)
								{
									itemPhysicalPath = repositoryManager.GetDuplicateCheckUniqueFileName(itemPhysicalPath);
									FileInfo renewFileInfo = new FileInfo(itemPhysicalPath);
									string renewFileName = renewFileInfo.Name;
									repositoryItem.ItemID = renewFileName;
									repositoryItem.FileName = renewFileName;
								}

								using (FileStream fileStream = new FileStream(itemPhysicalPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
								{
									await file.CopyToAsync(fileStream);
								}

								FileInfo fileInfo = new FileInfo(itemPhysicalPath);
								repositoryItem.PhysicalPath = itemPhysicalPath;
								repositoryItem.MD5 = GetFileMD5Hash(itemPhysicalPath);
								repositoryItem.CreationTime = fileInfo.CreationTime;
								repositoryItem.LastWriteTime = fileInfo.LastWriteTime;

								if (repository.IsVirtualPath.ParseBool() == true)
								{
									relativePath = string.Concat("/", repository.RepositoryID, "/");
									relativePath = relativePath + relativeDirectoryUrlPath + repositoryItem.FileName;
									absolutePath = "//" + Request.Host.Value + relativePath;
								}
								else
								{
									relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}";
									relativePath = Request.Path.Value.Replace("/UploadFile", "") + relativePath;
									absolutePath = "//" + Request.Host.Value + relativePath;
								}

								repositoryItem.RelativePath = relativePath;
								repositoryItem.AbsolutePath = absolutePath;

								break;
						}

						bool isDataUpsert = false;
						if (StaticConfig.IsLocalDB == true)
						{
							isDataUpsert = liteDBClient.Upsert(repositoryItem);
						}
						else
						{
							isDataUpsert = await businessApiClient.UpsertRepositoryItem(repositoryItem);
						}

						if (isDataUpsert == true)
						{
							result.ItemID = repositoryItem.ItemID;
							result.Result = true;
						}
						else
						{
							result.Message = "UpsertRepositoryItem 데이터 거래 오류";
							logger.Error("[{LogCategory}] " + $"{result.Message} - {JsonConvert.SerializeObject(repositoryItem)}", "FileManagerController/UploadFile");
						}

						#endregion
					}
					catch (Exception exception)
					{
						result.Message = exception.Message;
					}
				}
			}
			else
			{
				string xFileName = Request.Headers["X-File-Name"];
				string xFileSize = Request.Headers["X-File-Size"];

				if (string.IsNullOrEmpty(xFileName) == true || string.IsNullOrEmpty(xFileSize) == true)
				{
					result.Message = "업로드 파일 정보 없음";
				}
				else
				{
					try
					{
						xFileName = WebUtility.UrlDecode(xFileName);
						string fileName = string.IsNullOrEmpty(saveFileName) == true ? xFileName : saveFileName;
						long fileLength = xFileSize.GetLong();

						#region dropzone.js 업로드

						if (repository.UploadSizeLimit < ToFileLength(fileLength))
						{
							result.Message = repository.UploadSizeLimit.ToCurrencyString() + " 바이트 이상 업로드 할 수 없습니다";
							return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
						}

						RepositoryManager repositoryManager = new RepositoryManager();
						repositoryManager.PersistenceDirectoryPath = repositoryManager.GetPhysicalPath(repository, customPath1, customPath2, customPath3);
						string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, customPath1, customPath2, customPath3);
						string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
						relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";

						if (repository.IsMultiUpload.ParseBool() == true)
						{
							List<RepositoryItems> items = null;
							if (StaticConfig.IsLocalDB == true)
							{
								items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID).ToList();
							}
							else
							{
								items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID);
							}

							if (items != null)
							{
								if (items.Count > repository.UploadCount)
								{
									result.Message = repository.UploadCount.ToCurrencyString() + " 파일 갯수 이상 업로드 할 수 없습니다";
									return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
								}
							}

							result.RemainingCount = repository.UploadCount - (items.Count + 1);
						}
						else
						{
							List<RepositoryItems> items = null;
							if (StaticConfig.IsLocalDB == true)
							{
								items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID).ToList();
							}
							else
							{
								items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID);
							}

							if (items != null && items.Count() > 0)
							{
								BlobContainerClient container = null;
								bool hasContainer = false;
								if (repository.StorageType == "AzureBlob")
								{
									container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
									hasContainer = await container.ExistsAsync();
								}

								foreach (RepositoryItems item in items)
								{
									string deleteFileName;
									if (repository.IsFileNameEncrypt.ParseBool() == true)
									{
										deleteFileName = item.ItemID;
									}
									else
									{
										deleteFileName = item.FileName;
									}

									switch (repository.StorageType)
									{
										case "AzureBlob":
											if (hasContainer == true)
											{
												string blobID = relativeDirectoryUrlPath + deleteFileName;
												await container.DeleteBlobIfExistsAsync(blobID);
											}
											break;
										default:
											string filePath = relativeDirectoryPath + deleteFileName;
											repositoryManager.Delete(filePath);
											break;
									}

									if (StaticConfig.IsLocalDB == true)
									{
										liteDBClient.Delete<RepositoryItems>(p => p.ItemID == item.ItemID);
									}
									else
									{
										await businessApiClient.DeleteRepositoryItem(repositoryID, item.ItemID);
									}
								}
							}
						}

						string absolutePath = "";
						string relativePath = "";
						string policyPath = repositoryManager.GetPolicyPath(repository);
						string extension = Path.GetExtension(fileName);
						if (string.IsNullOrEmpty(extension) == true)
						{
							extension = Path.GetExtension(file.FileName);
						}

						repositoryItem = new RepositoryItems();
						repositoryItem.ItemID = repository.IsFileNameEncrypt.ParseBool() == true ? Guid.NewGuid().ToString().Replace("-", string.Empty).ToUpper() : fileName;
						repositoryItem.OrderBy = sequence;
						repositoryItem.ItemSummary = itemSummary;
						repositoryItem.FileName = fileName;
						repositoryItem.Extension = extension;
						repositoryItem.MimeType = GetMimeType(file.FileName);
						repositoryItem.FileLength = fileLength;
						repositoryItem.RepositoryID = repositoryID;
						repositoryItem.DependencyID = dependencyID;
						repositoryItem.CustomPath1 = customPath1;
						repositoryItem.CustomPath2 = customPath2;
						repositoryItem.CustomPath3 = customPath3;
						repositoryItem.PolicyPath = policyPath;
						repositoryItem.CreateUserID = userID;
						repositoryItem.CreateDateTime = DateTime.Now;

						switch (repository.StorageType)
						{
							case "AzureBlob":
								BlobContainerClient container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
								await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
								string blobID = relativeDirectoryUrlPath + repositoryItem.ItemID;
								BlobClient blob = container.GetBlobClient(blobID);

								if (repository.IsFileNameEncrypt.ParseBool() == false && repository.IsFileOverWrite.ParseBool() == false)
								{
									blobID = await repositoryManager.GetDuplicateCheckUniqueFileName(container, blobID);
									repositoryItem.ItemID = blobID;
									repositoryItem.FileName = blobID;
								}

								BlobHttpHeaders headers = new BlobHttpHeaders
								{
									ContentType = repositoryItem.MimeType
								};

								using (MemoryStream memoryStream = new MemoryStream(8192))
								{
									await Request.BodyReader.CopyToAsync(memoryStream);
									memoryStream.Position = 0;
									await blob.UploadAsync(memoryStream, headers);
									repositoryItem.MD5 = GetStreamMD5Hash(memoryStream);
								}

								BlobProperties properties = await blob.GetPropertiesAsync();

								repositoryItem.PhysicalPath = "";
								repositoryItem.CreationTime = properties.CreatedOn.LocalDateTime;
								repositoryItem.LastWriteTime = properties.LastModified.LocalDateTime;

								if (repository.IsVirtualPath.ParseBool() == true)
								{
									relativePath = relativeDirectoryUrlPath + repositoryItem.ItemID;
									if (string.IsNullOrEmpty(repository.AzureBlobItemUrl) == true)
									{
										absolutePath = $"//{repository.RepositoryID}.blob.core.windows.net/{repository.AzureBlobContainerID.ToLower()}/";
										absolutePath = absolutePath + relativePath;
									}
									else
									{
										absolutePath = repository.AzureBlobItemUrl
											.Replace("[CONTAINERID]", repository.AzureBlobContainerID.ToLower())
											.Replace("[BLOBID]", relativePath);
									}
								}
								else
								{
									relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}";
									relativePath = Request.Path.Value.Replace("/UploadFile", "") + relativePath;
									absolutePath = "//" + Request.Host.Value + relativePath;
								}
								break;
							default:
								string itemPhysicalPath = repositoryManager.GetSavePath(repositoryItem.ItemID);

								if (repository.IsFileNameEncrypt.ParseBool() == false && repository.IsFileOverWrite.ParseBool() == false)
								{
									itemPhysicalPath = repositoryManager.GetDuplicateCheckUniqueFileName(itemPhysicalPath);
									FileInfo renewFileInfo = new FileInfo(itemPhysicalPath);
									string renewFileName = renewFileInfo.Name;
									repositoryItem.ItemID = renewFileName;
									repositoryItem.FileName = renewFileName;
								}

								using (FileStream fileStream = new FileStream(itemPhysicalPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
								using (MemoryStream memoryStream = new MemoryStream(8192))
								{
									await Request.BodyReader.CopyToAsync(memoryStream);
									memoryStream.Position = 0;
									await memoryStream.CopyToAsync(fileStream);
								}

								FileInfo fileInfo = new FileInfo(itemPhysicalPath);
								repositoryItem.PhysicalPath = itemPhysicalPath;
								repositoryItem.MD5 = GetFileMD5Hash(itemPhysicalPath);
								repositoryItem.CreationTime = fileInfo.CreationTime;
								repositoryItem.LastWriteTime = fileInfo.LastWriteTime;

								if (repository.IsVirtualPath.ParseBool() == true)
								{
									relativePath = string.Concat("/", repository.RepositoryID, "/");
									relativePath = relativePath + relativeDirectoryUrlPath + repositoryItem.FileName;
									absolutePath = "//" + Request.Host.Value + relativePath;
								}
								else
								{
									relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}";
									relativePath = Request.Path.Value.Replace("/UploadFile", "") + relativePath;
									absolutePath = "//" + Request.Host.Value + relativePath;
								}

								repositoryItem.RelativePath = relativePath;
								repositoryItem.AbsolutePath = absolutePath;
								break;
						}

						bool isDataUpsert = false;
						if (StaticConfig.IsLocalDB == true)
						{
							isDataUpsert = liteDBClient.Upsert(repositoryItem);
						}
						else
						{
							isDataUpsert = await businessApiClient.UpsertRepositoryItem(repositoryItem);
						}

						if (isDataUpsert == true)
						{
							result.ItemID = repositoryItem.ItemID;
							result.Result = true;
						}
						else
						{
							result.Message = "UpsertRepositoryItem 데이터 거래 오류";
							logger.Error("[{LogCategory}] " + $"{result.Message} - {JsonConvert.SerializeObject(repositoryItem)}", "FileManagerController/UploadFile");
						}

						#endregion
					}
					catch (Exception exception)
					{
						result.Message = exception.Message;
					}
				}
			}

			var entity = new
			{
				ItemID = repositoryItem.ItemID,
				RepositoryID = repositoryItem.RepositoryID,
				DependencyID = repositoryItem.DependencyID,
				FileName = repositoryItem.FileName,
				Sequence = repositoryItem.OrderBy,
				AbsolutePath = repositoryItem.AbsolutePath,
				RelativePath = repositoryItem.RelativePath,
				Extension = repositoryItem.Extension,
				Size = repositoryItem.FileLength,
				MimeType = repositoryItem.MimeType,
				CustomPath1 = repositoryItem.CustomPath1,
				CustomPath2 = repositoryItem.CustomPath2,
				CustomPath3 = repositoryItem.CustomPath3,
				PolicyPath = repositoryItem.PolicyPath,
				MD5 = repositoryItem.MD5
			};

			Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
			Response.Headers["Qrame_ModelType"] = "FileItemResult";
			
			Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entity)));
			Response.Headers["X-Frame-Options"] = StaticConfig.XFrameOptions;
			return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
		}

		// http://localhost:7004/api/FileManager/UploadFiles
		[HttpPost("UploadFiles")]
		public async Task<ContentResult> UploadFiles(List<IFormFile> files)
		{
			MultiFileUploadResult result = new MultiFileUploadResult();
			result.Result = false;
			string elementID = Request.Query["ElementID"].ToString();
			string repositoryID = Request.Query["RepositoryID"].ToString();
			string dependencyID = Request.Query["DependencyID"].ToString();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(dependencyID) == true)
			{
				result.Message = "RepositoryID 또는 DependencyID 필수 요청 정보 필요";
				return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				result.Message = "RepositoryID 요청 정보 확인 필요";
				return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
			}

			string saveFileName = string.IsNullOrEmpty(Request.Query["fileName"]) == true ? "" : Request.Query["fileName"].ToString();
			string itemSummary = Request.Query["ItemSummary"].ToString();
			string customPath1 = Request.Query["CustomPath1"].ToString();
			string customPath2 = Request.Query["CustomPath2"].ToString();
			string customPath3 = Request.Query["CustomPath3"].ToString();
			string responseType = string.IsNullOrEmpty(Request.Query["responseType"]) == true ? "callback" : Request.Query["responseType"].ToString();
			string userID = string.IsNullOrEmpty(Request.Query["UserID"]) == true ? "system" : Request.Query["UserID"].ToString();
			string callback = string.IsNullOrEmpty(Request.Query["callback"]) == true ? "" : Request.Query["callback"].ToString();

			RepositoryItems repositoryItem = null;

			StringBuilder stringBuilder = new StringBuilder(512);
			string scriptStart = "<script type='text/javascript'>";
			string scriptEnd = "</script>";

			if (Request.HasFormContentType == true)
			{
				foreach (IFormFile file in files)
				{
					if (file == null || file.Length == 0)
					{
						stringBuilder.AppendLine(scriptStart);
						stringBuilder.AppendLine("alert('업로드 파일을 확인 할 수 없습니다');");
						stringBuilder.AppendLine("history.go(-1);");
						stringBuilder.AppendLine(scriptEnd);
						return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
					}
					else {
						if (repository.UploadSizeLimit < ToFileLength(file.Length))
						{
							stringBuilder.AppendLine(scriptStart);
							stringBuilder.AppendLine("alert('" + repository.UploadSizeLimit.ToCurrencyString() + "이상 업로드 할 수 없습니다');");
							stringBuilder.AppendLine("history.go(-1);");
							stringBuilder.AppendLine(scriptEnd);
							return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
						}
					}
				}

				RepositoryManager repositoryManager = new RepositoryManager();
				repositoryManager.PersistenceDirectoryPath = repositoryManager.GetPhysicalPath(repository, customPath1, customPath2, customPath3);
				string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, customPath1, customPath2, customPath3);
				string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
				relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";
				string policyPath = repositoryManager.GetPolicyPath(repository);

				if (repository.IsMultiUpload.ParseBool() == true)
				{
					List<RepositoryItems> items = null;
					if (StaticConfig.IsLocalDB == true)
					{
						items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID).ToList();
					}
					else
					{
						items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID);
					}

					if (items != null && items.Count > 0)
					{
						if ((items.Count + files.Count) > repository.UploadCount)
						{
							stringBuilder.AppendLine(scriptStart);
							stringBuilder.AppendLine("alert('" + repository.UploadCount.ToCurrencyString() + " 파일 갯수 이상 업로드 할 수 없습니다');");
							stringBuilder.AppendLine("history.go(-1);");
							stringBuilder.AppendLine(scriptEnd);
							return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
						}
					}

					result.RemainingCount = repository.UploadCount - (items.Count + files.Count);
				}
				else
				{
					List<RepositoryItems> items = null;
					if (StaticConfig.IsLocalDB == true)
					{
						items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID).ToList();
					}
					else
					{
						items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID);
					}

					if (items != null && items.Count() > 0)
					{
						BlobContainerClient container = null;
						bool hasContainer = false;
						if (repository.StorageType == "AzureBlob")
						{
							container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
							hasContainer = await container.ExistsAsync();
						}

						foreach (RepositoryItems item in items)
						{
							string deleteFileName;
							if (repository.IsFileNameEncrypt.ParseBool() == true)
							{
								deleteFileName = item.ItemID;
							}
							else
							{
								deleteFileName = item.FileName;
							}

							switch (repository.StorageType)
							{
								case "AzureBlob":
									if (hasContainer == true)
									{
										string blobID = relativeDirectoryUrlPath + deleteFileName;
										await container.DeleteBlobIfExistsAsync(blobID);
									}
									break;
								default:
									repositoryManager.Delete(deleteFileName);
									break;
							}

							if (StaticConfig.IsLocalDB == true)
							{
								liteDBClient.Delete<RepositoryItems>(p => p.ItemID == item.ItemID);
							}
							else
							{
								await businessApiClient.DeleteRepositoryItem(repositoryID, item.ItemID);
							}
						}
					}
				}

				int sequence = 1;
				foreach (IFormFile file in files)
				{
					FileUploadResult fileUploadResult = new FileUploadResult();
					fileUploadResult.Result = false;
					if (file == null)
					{
						result.Message = "업로드 파일 정보 없음";
					}
					else
					{
						try
						{
							string absolutePath = "";
							string relativePath = "";
							string fileName = string.IsNullOrEmpty(saveFileName) == true ? file.FileName : saveFileName;
							string extension = Path.GetExtension(fileName);
							if (string.IsNullOrEmpty(extension) == true) {
								extension = Path.GetExtension(file.FileName);
							}

							repositoryItem = new RepositoryItems();
							repositoryItem.ItemID = repository.IsFileNameEncrypt.ParseBool() == true ? Guid.NewGuid().ToString().Replace("-", string.Empty).ToUpper() : fileName;
							repositoryItem.OrderBy = sequence;
							repositoryItem.ItemSummary = itemSummary;
							repositoryItem.FileName = fileName;
							repositoryItem.Extension = extension;
							repositoryItem.MimeType = GetMimeType(file.FileName);
							repositoryItem.FileLength = file.Length;
							repositoryItem.RepositoryID = repositoryID;
							repositoryItem.DependencyID = dependencyID;
							repositoryItem.CustomPath1 = customPath1;
							repositoryItem.CustomPath2 = customPath2;
							repositoryItem.CustomPath3 = customPath3;
							repositoryItem.PolicyPath = policyPath;
							repositoryItem.CreateUserID = userID;
							repositoryItem.CreateDateTime = DateTime.Now;

							switch (repository.StorageType)
							{
								case "AzureBlob":
									BlobContainerClient container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
									await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
									string blobID = relativeDirectoryUrlPath + repositoryItem.ItemID;
									BlobClient blob = container.GetBlobClient(blobID);

									if (repository.IsFileNameEncrypt.ParseBool() == false && repository.IsFileOverWrite.ParseBool() == false)
									{
										blobID = await repositoryManager.GetDuplicateCheckUniqueFileName(container, blobID);
										repositoryItem.ItemID = blobID;
										repositoryItem.FileName = blobID;
									}

									Stream openReadStream = file.OpenReadStream();
									BlobHttpHeaders headers = new BlobHttpHeaders
									{
										ContentType = repositoryItem.MimeType
									};

									await blob.UploadAsync(openReadStream, headers);

									BlobProperties properties = await blob.GetPropertiesAsync();

									repositoryItem.PhysicalPath = "";
									repositoryItem.MD5 = GetStreamMD5Hash(openReadStream);
									repositoryItem.CreationTime = properties.CreatedOn.LocalDateTime;
									repositoryItem.LastWriteTime = properties.LastModified.LocalDateTime;

									if (repository.IsVirtualPath.ParseBool() == true)
									{
										relativePath = relativeDirectoryUrlPath + repositoryItem.ItemID;
										if (string.IsNullOrEmpty(repository.AzureBlobItemUrl) == true)
										{
											absolutePath = $"//{repository.RepositoryID}.blob.core.windows.net/{repository.AzureBlobContainerID.ToLower()}/";
											absolutePath = absolutePath + relativePath;
										}
										else
										{
											absolutePath = repository.AzureBlobItemUrl
												.Replace("[CONTAINERID]", repository.AzureBlobContainerID.ToLower())
												.Replace("[BLOBID]", relativePath);
										}
									}
									else
									{
										relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}";
										relativePath = Request.Path.Value.Replace("/UploadFiles", "") + relativePath;
										absolutePath = "//" + Request.Host.Value + relativePath;
									}

									repositoryItem.RelativePath = relativePath;
									repositoryItem.AbsolutePath = absolutePath;
									break;
								default:
									string itemPhysicalPath = repositoryManager.GetSavePath(repositoryItem.ItemID);

									if (repository.IsFileNameEncrypt.ParseBool() == false && repository.IsFileOverWrite.ParseBool() == false)
									{
										itemPhysicalPath = repositoryManager.GetDuplicateCheckUniqueFileName(itemPhysicalPath);
										FileInfo renewFileInfo = new FileInfo(itemPhysicalPath);
										string renewFileName = renewFileInfo.Name;
										repositoryItem.ItemID = renewFileName;
										repositoryItem.FileName = renewFileName;
									}

									using (FileStream fileStream = new FileStream(itemPhysicalPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
									{
										await file.CopyToAsync(fileStream);
									}

									FileInfo fileInfo = new FileInfo(itemPhysicalPath);
									repositoryItem.PhysicalPath = itemPhysicalPath;
									repositoryItem.MD5 = GetFileMD5Hash(itemPhysicalPath);
									repositoryItem.CreationTime = fileInfo.CreationTime;
									repositoryItem.LastWriteTime = fileInfo.LastWriteTime;

									if (repository.IsVirtualPath.ParseBool() == true)
									{
										relativePath = string.Concat("/", repository.RepositoryID, "/");
										relativePath = relativePath + relativeDirectoryUrlPath + repositoryItem.FileName;
										absolutePath = "//" + Request.Host.Value + relativePath;
									}
									else
									{
										relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}";
										relativePath = Request.Path.Value.Replace("/UploadFiles", "") + relativePath;
										absolutePath = "//" + Request.Host.Value + relativePath;
									}

									repositoryItem.RelativePath = relativePath;
									repositoryItem.AbsolutePath = absolutePath;
									break;
							}

							bool isDataUpsert = false;
							if (StaticConfig.IsLocalDB == true)
							{
								isDataUpsert = liteDBClient.Upsert(repositoryItem);
							}
							else
							{
								isDataUpsert = await businessApiClient.UpsertRepositoryItem(repositoryItem);
							}

							if (isDataUpsert == true)
							{
								fileUploadResult.ItemID = repositoryItem.ItemID;
								fileUploadResult.Result = true;
							}
							else
							{
								fileUploadResult.Message = "UpsertRepositoryItem 데이터 거래 오류";
								logger.Error("[{LogCategory}] " + $"{result.Message} - {JsonConvert.SerializeObject(repositoryItem)}", "FileManagerController/UploadFiles");
							}
						}
						catch (Exception exception)
						{
							fileUploadResult.Message = exception.Message;
						}
					}

					result.FileUploadResults.Add(fileUploadResult);
					sequence = sequence + 1;
				}

				result.Result = true;
			}
			else
			{
				stringBuilder.AppendLine(scriptStart);
				stringBuilder.AppendLine("alert('잘못된 파일 업로드 요청');");
				stringBuilder.AppendLine("history.go(-1);");
				stringBuilder.AppendLine(scriptEnd);
				return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
			}

			List<RepositoryItems> repositoryItems = null;
			if (StaticConfig.IsLocalDB == true)
			{
				repositoryItems = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID).ToList();
			}
			else
			{
				repositoryItems = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID);
			}

			if (repositoryItems != null && repositoryItems.Count > 0 && string.IsNullOrEmpty(callback) == false)
			{
				if (responseType == "callback")
				{
					stringBuilder.AppendLine(scriptStart);
					stringBuilder.AppendLine("var elementID = '" + elementID + "';");
					stringBuilder.AppendLine("var callback = '" + callback + "';");
					stringBuilder.AppendLine("var repositoryID = '" + repositoryID + "';");
					stringBuilder.AppendLine("var repositoryItems = [];");

					for (int i = 0; i < repositoryItems.Count; i++)
					{
						var item = repositoryItems[i];
						var entity = new
						{
							ItemID = item.ItemID,
							RepositoryID = item.RepositoryID,
							DependencyID = item.DependencyID,
							FileName = item.FileName,
							Sequence = item.OrderBy,
							AbsolutePath = item.AbsolutePath,
							RelativePath = item.RelativePath,
							Extension = item.Extension,
							Size = item.FileLength,
							MimeType = item.MimeType,
							CustomPath1 = item.CustomPath1,
							CustomPath2 = item.CustomPath2,
							CustomPath3 = item.CustomPath3,
							PolicyPath = item.PolicyPath,
							MD5 = item.MD5
						};

						stringBuilder.AppendLine("repositoryItems.push(" + Qrame.CoreFX.Data.JsonConverter.Serialize(entity) + ");");
					}

					// stringBuilder.AppendLine("parent." + callback + "(repositoryID, repositoryItems);");
					stringBuilder.AppendLine("parent.postMessage({action: 'UploadFiles', elementID: elementID, callback: callback, repositoryID: repositoryID, repositoryItems: repositoryItems}, '*');");
					stringBuilder.AppendLine(scriptEnd);

					return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
				}
				else if (responseType == "json")
				{
					List<dynamic> entitys = new List<dynamic>();
					for (int i = 0; i < repositoryItems.Count; i++)
					{
						var item = repositoryItems[i];
						var entity = new
						{
							ItemID = item.ItemID,
							RepositoryID = item.RepositoryID,
							DependencyID = item.DependencyID,
							FileName = item.FileName,
							Sequence = item.OrderBy,
							AbsolutePath = item.AbsolutePath,
							RelativePath = item.RelativePath,
							Extension = item.Extension,
							Size = item.FileLength,
							MimeType = item.MimeType,
							CustomPath1 = item.CustomPath1,
							CustomPath2 = item.CustomPath2,
							CustomPath3 = item.CustomPath3,
							PolicyPath = item.PolicyPath,
							MD5 = item.MD5
						};

						entitys.Add(entity);
					}

					Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
					Response.Headers["Qrame_ModelType"] = "MultiFileItemResult";

					Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entitys)));
					Response.Headers["X-Frame-Options"] = StaticConfig.XFrameOptions;
					return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
				}
				else
				{
					return Content("", "text/html", Encoding.UTF8);
				}
			}
			else
			{
				stringBuilder.AppendLine(scriptStart);
				stringBuilder.AppendLine("alert('잘못된 파일 업로드 요청');");
				stringBuilder.AppendLine("history.go(-1);");
				stringBuilder.AppendLine(scriptEnd);
				return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
			}
		}

		// http://localhost:7004/api/FileManager/DownloadFile
		[HttpPost("DownloadFile")]
		public async Task<ActionResult> DownloadFile(DownloadRequest downloadRequest)
		{
			ActionResult result = NotFound();

			DownloadResult downloadResult = new DownloadResult();
			downloadResult.Result = false;

			string repositoryID = downloadRequest.RepositoryID;
			string itemID = downloadRequest.ItemID;
			string fileMD5 = downloadRequest.FileMD5;
			string tokenID = downloadRequest.TokenID;

			// 보안 검증 처리

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				downloadResult.Message = "RepositoryID 또는 ItemID 필수 요청 정보 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				downloadResult.Message = "RepositoryID 요청 정보 확인 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			switch (repository.StorageType)
			{
				case "AzureBlob":
					result = await ExecuteBlobFileDownload(downloadResult, repositoryID, itemID);
					break;
				default:
					result = await ExecuteFileDownload(downloadResult, repositoryID, itemID);
					break;
			}

			Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
			Response.Headers["Qrame_ModelType"] = "DownloadResult";
			
			Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(downloadResult)));
			Response.Headers["X-Frame-Options"] = StaticConfig.XFrameOptions;
			return result;
		}

		// http://localhost:7004/api/FileManager/HttpDownloadFile?repositoryid=2FD91746-D77A-4EE1-880B-14AA604ACE5A&itemID=
		[HttpGet("HttpDownloadFile")]
		public async Task<ActionResult> HttpDownloadFile(string repositoryID, string itemID, string fileMD5, string tokenID)
		{
			ActionResult result = NotFound();

			DownloadResult downloadResult = new DownloadResult();
			downloadResult.Result = false;

			// 보안 검증 처리

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				downloadResult.Message = "RepositoryID 또는 ItemID 필수 요청 정보 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				downloadResult.Message = "RepositoryID 요청 정보 확인 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			switch (repository.StorageType)
			{
				case "AzureBlob":
					result = await ExecuteBlobFileDownload(downloadResult, repositoryID, itemID);
					break;
				default:
					result = await ExecuteFileDownload(downloadResult, repositoryID, itemID);
					break;
			}

			Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
			Response.Headers["Qrame_ModelType"] = "DownloadResult";
			
			Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(downloadResult)));
			Response.Headers["X-Frame-Options"] = StaticConfig.XFrameOptions;
			return result;
		}

		// http://localhost:7004/api/FileManager/VirtualDownloadFile?repositoryid=2FD91746-D77A-4EE1-880B-14AA604ACE5A&filename=강아지.jpg&subdirectory=2020
		[HttpGet("VirtualDownloadFile")]
		public async Task<ActionResult> VirtualDownloadFile(string repositoryID, string fileName, string subDirectory)
		{
			ActionResult result = NotFound();

			DownloadResult downloadResult = new DownloadResult();
			downloadResult.Result = false;

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(fileName) == true)
			{
				downloadResult.Message = "RepositoryID 또는 fileName 필수 요청 정보 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				downloadResult.Message = "RepositoryID 요청 정보 확인 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			result = await VirtualFileDownload(downloadResult, repositoryID, fileName, subDirectory);

			Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
			Response.Headers["Qrame_ModelType"] = "DownloadResult";
			
			Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(downloadResult)));
			Response.Headers["X-Frame-Options"] = StaticConfig.XFrameOptions;
			return result;
		}

		// http://localhost:7004/api/FileManager/GetRepositorys
		[HttpGet("GetRepositorys")]
		public string GetRepositorys()
		{
			string result = "";
			try
			{
				if (StaticConfig.FileRepositorys != null)
				{
					List<Repository> repositories = new List<Repository>();
                    for (int i = 0; i < StaticConfig.FileRepositorys.Count; i++)
                    {
						Repository repository = StaticConfig.FileRepositorys[i];
						repositories.Add(new Repository() {
							RepositoryID = repository.RepositoryID,
							RepositoryName = repository.RepositoryName,
							ApplicationID = repository.ApplicationID,
							ProjectID = repository.ProjectID,
							StorageType = repository.StorageType,
							PhysicalPath = repository.PhysicalPath,
							AzureBlobContainerID = repository.AzureBlobContainerID,
							AzureBlobItemUrl = repository.AzureBlobItemUrl,
							IsVirtualPath = repository.IsVirtualPath,
							IsMultiUpload = repository.IsMultiUpload,
							IsFileOverWrite = repository.IsFileOverWrite,
							IsFileNameEncrypt = repository.IsFileNameEncrypt,
							IsAutoPath = repository.IsAutoPath,
							PolicyPathID = repository.PolicyPathID,
							UploadTypeID = repository.UploadTypeID,
							UploadType = repository.UploadType,
							UploadExtensions = repository.UploadExtensions,
							UploadCount = repository.UploadCount,
							UploadSizeLimit = repository.UploadSizeLimit,
							PolicyExceptionID = repository.PolicyExceptionID,
							RedirectUrl = repository.RedirectUrl,
							TransactionGetItem = repository.TransactionGetItem,
							TransactionGetItems = repository.TransactionGetItems,
							TransactionDeleteItem = repository.TransactionDeleteItem,
							TransactionUpsertItem = repository.TransactionUpsertItem,
							TransactionUpdateDendencyID = repository.TransactionUpdateDendencyID,
							TransactionUpdateFileName = repository.TransactionUpdateFileName,
							UseYN = repository.UseYN,
							Description = repository.Description,
							CreateUserID = repository.CreateUserID,
							CreateDateTime = repository.CreateDateTime,
							UpdateUserID = repository.UpdateUserID,
							UpdateDateTime = repository.UpdateDateTime
						});
					}
					result = JsonConvert.SerializeObject(repositories);
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "FileManagerController/GetRepositorys");
			}

			return result;
		}

		// http://localhost:7004/api/FileManager/RemoveItem?repositoryID=AttachFile&itemid=12345678
		[HttpGet("RemoveItem")]
		public async Task<ContentResult> RemoveItem(string repositoryID, string itemID)
		{
			JsonContentResult jsonContentResult = new JsonContentResult();
			jsonContentResult.Result = false;

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				jsonContentResult.Message = "RepositoryID 또는 ItemID 필수 요청 정보 필요";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/RemoveItem");
				return Content(JsonConvert.SerializeObject(jsonContentResult), "application/json", Encoding.UTF8);
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				jsonContentResult.Message = "RepositoryID 요청 정보 확인 필요";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/RemoveItem");
				return Content(JsonConvert.SerializeObject(jsonContentResult), "application/json", Encoding.UTF8);
			}

			try
			{
				RepositoryItems repositoryItem = null;
				if (StaticConfig.IsLocalDB == true)
				{
					repositoryItem = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.ItemID == itemID).FirstOrDefault();
				}
				else
				{
					repositoryItem = await businessApiClient.GetRepositoryItem(repositoryID, itemID);
				}

				if (repositoryItem != null)
				{
					RepositoryManager repositoryManager = new RepositoryManager();
					repositoryManager.PersistenceDirectoryPath = repositoryManager.GetRepositoryItemPath(repository, repositoryItem);
					string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, repositoryItem.CustomPath1, repositoryItem.CustomPath2, repositoryItem.CustomPath3);
					string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
					relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";

					BlobContainerClient container = null;
					bool hasContainer = false;
					if (repository.StorageType == "AzureBlob")
					{
						container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
						hasContainer = await container.ExistsAsync();
					}

					string fileName;
					if (repository.IsFileNameEncrypt.ParseBool() == true)
					{
						fileName = repositoryItem.ItemID;
					}
					else
					{
						fileName = repositoryItem.FileName;
					}

					switch (repository.StorageType)
					{
						case "AzureBlob":
							if (hasContainer == true)
							{
								string blobID = relativeDirectoryUrlPath + fileName;
								await container.DeleteBlobIfExistsAsync(blobID);
							}
							break;
						default:
							string filePath = relativeDirectoryPath + fileName;
							repositoryManager.Delete(filePath);
							break;
					}

					if (StaticConfig.IsLocalDB == true)
					{
						liteDBClient.Delete<RepositoryItems>(p => p.ItemID == repositoryItem.ItemID);
					}
					else
					{
						await businessApiClient.DeleteRepositoryItem(repositoryID, repositoryItem.ItemID);
					}

					jsonContentResult.Result = true;
				}
				else
				{
					jsonContentResult.Message = $"ItemID: '{itemID}' 파일 요청 정보 확인 필요";
					logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/RemoveItem");
				}
			}
			catch (Exception exception)
			{
				jsonContentResult.Message = exception.Message;
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "FileManagerController/RemoveItem");
			}

			return Content(JsonConvert.SerializeObject(jsonContentResult), "application/json", Encoding.UTF8);
		}

		// http://localhost:7004/api/FileManager/RemoveItems?repositoryID=AttachFile&dependencyID=helloworld
		[HttpGet("RemoveItems")]
		public async Task<ContentResult> RemoveItems(string repositoryID, string dependencyID)
		{
			JsonContentResult jsonContentResult = new JsonContentResult();
			jsonContentResult.Result = false;

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(dependencyID) == true)
			{
				jsonContentResult.Message = "RepositoryID 또는 DependencyID 필수 요청 정보 필요";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/RemoveItems");
				return Content(JsonConvert.SerializeObject(jsonContentResult), "application/json", Encoding.UTF8);
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);

			if (repository == null)
			{
				jsonContentResult.Message = "RepositoryID 정보 확인 필요";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/RemoveItems");
				return Content(JsonConvert.SerializeObject(jsonContentResult), "application/json", Encoding.UTF8);
			}

			try
			{
				List<RepositoryItems> repositoryItems = null;
				if (StaticConfig.IsLocalDB == true)
				{
					repositoryItems = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID).ToList();
				}
				else
				{
					repositoryItems = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID);
				}

				if (repositoryItems != null && repositoryItems.Count > 0)
				{
					RepositoryManager repositoryManager = new RepositoryManager();

					BlobContainerClient container = null;
					bool hasContainer = false;
					if (repository.StorageType == "AzureBlob")
					{
						container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
						hasContainer = await container.ExistsAsync();
					}

					foreach (var repositoryItem in repositoryItems)
					{
						repositoryManager.PersistenceDirectoryPath = repositoryManager.GetRepositoryItemPath(repository, repositoryItem);
						string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, repositoryItem.CustomPath1, repositoryItem.CustomPath2, repositoryItem.CustomPath3);
						string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
						relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";

						string fileName;

						if (repository.IsFileNameEncrypt.ParseBool() == true)
						{
							fileName = repositoryItem.ItemID;
						}
						else
						{
							fileName = repositoryItem.FileName;
						}

						switch (repository.StorageType)
						{
							case "AzureBlob":
								if (hasContainer == true)
								{
									string blobID = relativeDirectoryUrlPath + fileName;
									await container.DeleteBlobIfExistsAsync(blobID);
								}
								break;
							default:
								string filePath = relativeDirectoryPath + fileName;
								repositoryManager.Delete(filePath);
								break;
						}

						if (StaticConfig.IsLocalDB == true)
						{
							liteDBClient.Delete<RepositoryItems>(p => p.ItemID == repositoryItem.ItemID);
						}
						else
						{
							await businessApiClient.DeleteRepositoryItem(repositoryID, repositoryItem.ItemID);
						}
					}

					jsonContentResult.Result = true;
				}
				else
				{
					jsonContentResult.Message = $"DependencyID: '{dependencyID}' 파일 요청 정보 확인 필요";
					logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/RemoveItems");
				}
			}
			catch (Exception exception)
			{
				jsonContentResult.Message = exception.Message;
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "FileManagerController/RemoveItems");
			}

			return Content(JsonConvert.SerializeObject(jsonContentResult), "application/json", Encoding.UTF8);
		}

		private async Task<ActionResult> VirtualFileDownload(DownloadResult downloadResult, string repositoryID, string fileName, string subDirectory = "")
		{
			ActionResult result;

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(fileName) == true)
			{
				downloadResult.Message = "RepositoryID 또는 fileName 필수 요청 정보 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);

			if (repository == null)
			{
				downloadResult.Message = "RepositoryID 정보 확인 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			if (repository.IsVirtualPath.ParseBool() == false)
			{
				downloadResult.Message = "Virtual 다운로드 지원 안함";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			RepositoryManager repositoryManager = new RepositoryManager();

			if (repository.StorageType == "AzureBlob")
			{
				BlobContainerClient container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
				await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
				string blobID = (string.IsNullOrEmpty(subDirectory) == false ? subDirectory + "/" : "") + fileName;
				BlobClient blob = container.GetBlobClient(blobID);
				if (await blob.ExistsAsync() == true)
				{
					BlobDownloadInfo blobDownloadInfo = await blob.DownloadAsync();
					result = File(blobDownloadInfo.Content, blobDownloadInfo.ContentType, fileName);

					BlobProperties properties = await blob.GetPropertiesAsync();
					downloadResult.FileName = fileName;
					downloadResult.MimeType = properties.ContentType;
					downloadResult.MD5 = properties.ContentHash.ToBase64String();
					downloadResult.Length = properties.ContentLength;
					downloadResult.CreationTime = properties.CreatedOn.LocalDateTime;
					downloadResult.LastWriteTime = properties.LastModified.LocalDateTime;
					downloadResult.Result = true;
				}
				else
				{
					result = NotFound();
					downloadResult.Message = $"파일을 찾을 수 없습니다. FileID - '{blobID}'";
				}
			}
			else
			{
				if (string.IsNullOrEmpty(subDirectory) == true)
				{
					repositoryManager.PersistenceDirectoryPath = repository.PhysicalPath;
				}
				else
				{
					repositoryManager.PersistenceDirectoryPath = Path.Combine(repository.PhysicalPath, subDirectory);
				}

				var filePath = Path.Combine(repositoryManager.PersistenceDirectoryPath, fileName);
				try
				{
					if (System.IO.File.Exists(filePath) == true)
					{
						string mimeType = GetMimeType(fileName);

						MemoryStream memory = new MemoryStream(8192);
						using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
						{
							await stream.CopyToAsync(memory);
							memory.Position = 0;
						}

						result = File(memory, mimeType, fileName);

						FileInfo fileInfo = new FileInfo(filePath);
						downloadResult.FileName = fileName;
						downloadResult.MimeType = mimeType;
						downloadResult.MD5 = GetFileMD5Hash(filePath);
						downloadResult.Length = fileInfo.Length;
						downloadResult.CreationTime = fileInfo.CreationTime;
						downloadResult.LastWriteTime = fileInfo.LastWriteTime;
						downloadResult.Result = true;
					}
					else
					{
						result = NotFound();
						downloadResult.Message = $"파일을 찾을 수 없습니다. fileName - '{fileName}', subDirectory - '{subDirectory}'";
					}
				}
				catch (Exception exception)
				{
					result = StatusCode(500, exception.ToMessage());
					downloadResult.Message = $"파일을 다운로드 중 오류가 발생했습니다. fileName - '{fileName}', subDirectory - '{subDirectory}', message - '{exception.Message}'";
					logger.Error("[{LogCategory}] " + $"{downloadResult.Message} - {exception.ToMessage()}", "FileManagerController/VirtualFileDownload");
				}
			}

			return result;
		}

		private async Task<ActionResult> ExecuteBlobFileDownload(DownloadResult downloadResult, string repositoryID, string itemID)
		{
			ActionResult result = NotFound();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				downloadResult.Message = "RepositoryID 또는 itemID 필수 요청 정보 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);

			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				downloadResult.Message = "RepositoryID 정보 확인 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			RepositoryItems repositoryItem = null;
			if (StaticConfig.IsLocalDB == true)
			{
				repositoryItem = liteDBClient.Select<RepositoryItems>(p => p.ItemID == itemID).FirstOrDefault();
			}
			else
			{
				repositoryItem = await businessApiClient.GetRepositoryItem(repositoryID, itemID);
			}

			if (repositoryItem == null)
			{
				downloadResult.Message = "파일 정보 없음";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			RepositoryManager repositoryManager = new RepositoryManager();
			string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, repositoryItem.CustomPath1, repositoryItem.CustomPath2, repositoryItem.CustomPath3);
			string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
			relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";

			BlobContainerClient container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
			await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

			string fileName;
			if (repository.IsFileNameEncrypt.ParseBool() == true)
			{
				fileName = repositoryItem.ItemID;
			}
			else
			{
				fileName = repositoryItem.FileName;
			}

			string blobID = relativeDirectoryUrlPath + fileName;

			BlobClient blob = container.GetBlobClient(blobID);
			if (await blob.ExistsAsync() == true)
			{
				BlobDownloadInfo blobDownloadInfo = await blob.DownloadAsync();

				MemoryStream memory = new MemoryStream(8192);
				await blobDownloadInfo.Content.CopyToAsync(memory);
				memory.Position = 0;

				string downloadFileName;
				if (string.IsNullOrEmpty(Path.GetExtension(repositoryItem.FileName)) == true && string.IsNullOrEmpty(repositoryItem.Extension) == false) {
					downloadFileName = string.Concat(repositoryItem.FileName, repositoryItem.Extension);
				}
				else {
					downloadFileName = repositoryItem.FileName;
				}

				result = File(memory, repositoryItem.MimeType, downloadFileName);

				downloadResult.FileName = downloadFileName;
				downloadResult.MimeType = repositoryItem.MimeType;
				downloadResult.MD5 = repositoryItem.MD5;
				downloadResult.Length = repositoryItem.FileLength;
				downloadResult.CreationTime = repositoryItem.CreationTime;
				downloadResult.LastWriteTime = repositoryItem.LastWriteTime;
				downloadResult.Result = true;
			}
			else
			{
				result = NotFound();
				downloadResult.Message = $"파일을 찾을 수 없습니다. FileID - '{itemID}'";
			}

			return result;
		}

		private async Task<ActionResult> ExecuteFileDownload(DownloadResult downloadResult, string repositoryID, string itemID)
		{
			ActionResult result = NotFound();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				downloadResult.Message = "RepositoryID 또는 itemID 필수 요청 정보 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);

			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				downloadResult.Message = "RepositoryID 정보 확인 필요";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			RepositoryItems repositoryItem = null;
			if (StaticConfig.IsLocalDB == true)
			{
				repositoryItem = liteDBClient.Select<RepositoryItems>(p => p.ItemID == itemID).FirstOrDefault();
			}
			else
			{
				repositoryItem = await businessApiClient.GetRepositoryItem(repositoryID, itemID);
			}

			if (repositoryItem == null)
			{
				downloadResult.Message = "파일 정보 없음";
				return result;
			}

			try
			{
				var filePath = repositoryItem.PhysicalPath;
				if (System.IO.File.Exists(filePath) == true)
				{
					string mimeType = repositoryItem.MimeType;
					if (string.IsNullOrEmpty(mimeType) == true)
					{
						mimeType = GetMimeType(repositoryItem.FileName);
					}

					MemoryStream memory = new MemoryStream(8192);
					using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						await stream.CopyToAsync(memory);
						memory.Position = 0;
					}

					string downloadFileName;
					if (string.IsNullOrEmpty(Path.GetExtension(repositoryItem.FileName)) == true && string.IsNullOrEmpty(repositoryItem.Extension) == false)
					{
						downloadFileName = string.Concat(repositoryItem.FileName, repositoryItem.Extension);
					}
					else
					{
						downloadFileName = repositoryItem.FileName;
					}

					result = File(memory, mimeType, downloadFileName);

					FileInfo fileInfo = new FileInfo(filePath);
					downloadResult.FileName = downloadFileName;
					downloadResult.MimeType = mimeType;
					downloadResult.MD5 = repositoryItem.MD5;
					downloadResult.Length = repositoryItem.FileLength;
					downloadResult.CreationTime = repositoryItem.CreationTime;
					downloadResult.LastWriteTime = repositoryItem.LastWriteTime;
					downloadResult.Result = true;
				}
				else
				{
					result = NotFound();
					downloadResult.Message = $"파일을 찾을 수 없습니다. FileID - '{itemID}'";
				}
			}
			catch (Exception exception)
			{
				result = StatusCode(500, exception.ToMessage());
				downloadResult.Message = $"파일을 다운로드 중 오류가 발생했습니다. FileID - '{itemID}', '{exception.Message}'";
				logger.Error("[{LogCategory}] " + $"{downloadResult.Message} - {exception.ToMessage()}", "FileManagerController/ExecuteFileDownload");
			}

			return result;
		}

		// http://localhost:7004/api/FileManager/GetMimeType?path=test.json
		[HttpGet("GetMimeType")]
		public string GetMimeType(string path)
		{
			string result = MimeHelper.GetMimeType(Path.GetFileName(path));
			if (string.IsNullOrEmpty(result) == true)
			{
				result = "application/octet-stream";
			}

			return result;
		}

		private string GetFileMD5Hash(string filePath)
		{
			using (var md5 = MD5.Create())
			using (var stream = System.IO.File.OpenRead(filePath))
			{
				return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty);
			}
		}

		private string GetStreamMD5Hash(Stream fileStream)
		{
			using (var md5 = MD5.Create())
			{
				return BitConverter.ToString(md5.ComputeHash(fileStream)).Replace("-", string.Empty);
			}
		}

		// http://localhost:7004/api/FileManager/GetMD5Hash?value=s
		[HttpGet("GetMD5Hash")]
		public string GetMD5Hash(string value)
		{
			using (var md5 = MD5.Create())
			{
				return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
			}
		}

		private long ToFileLength(long fileLength)
		{
			long result = 0;
			if (fileLength < 0)
			{
				fileLength = 0;
			}

			if (fileLength < 1048576.0)
			{
				result = (fileLength / 1024);
			}
			if (fileLength < 1073741824.0)
			{
				result = (fileLength / 1024) / 1024;
			}

			return result;
		}

	}
}
