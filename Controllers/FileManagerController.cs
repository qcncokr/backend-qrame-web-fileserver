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

		// http://localhost:8004/api/FileManager/GetToken?remoteIP=localhost
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

		// http://localhost:8004/api/FileManager/RequestIP
		[HttpGet("RequestIP")]
		public string GetClientIP()
		{
			return HttpContext.Features.Get<IHttpConnectionFeature>()?.RemoteIpAddress?.ToString();
		}

		// http://localhost:8004/api/FileManager/ActionHandler
		[HttpGet("ActionHandler")]
		public async Task<ActionResult> ActionHandler()
		{
			ActionResult result = NotFound();

			JsonContentResult jsonContentResult = new JsonContentResult();
			jsonContentResult.Result = false;

			string action = Request.Query["Action"].ToString();

			switch (action.ToUpper())
			{
				case "GETITEM":
					result = await GetItem(jsonContentResult);
					break;
				case "GETITEMS":
					result = await GetItems(jsonContentResult);
					break;
				case "UPDATEDEPENDENCYID":
					result = await UpdateDependencyID(jsonContentResult);
					break;
				case "UPDATEFILENAME":
					result = await UpdateFileName(jsonContentResult);
					break;
			}

			Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
			Response.Headers["Qrame_ModelType"] = "JsonContentResult";
			
			Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jsonContentResult)));
			return result;
		}

		private async Task<ActionResult> UpdateDependencyID(JsonContentResult jsonContentResult)
		{
			ActionResult result = null;
			string repositoryID = Request.Query["RepositoryID"].ToString();
			string sourceDependencyID = Request.Query["SourceDependencyID"].ToString();
			string targetDependencyID = Request.Query["TargetDependencyID"].ToString();
			string businessID = string.IsNullOrEmpty(Request.Query["BusinessID"]) == true ? "" : Request.Query["BusinessID"].ToString();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(sourceDependencyID) == true || string.IsNullOrEmpty(targetDependencyID) == true)
			{
				string message = $"UPDATEDEPENDENCYID ?????? ????????? ???????????? ????????????, repositoryID: {repositoryID}, sourceDependencyID: {sourceDependencyID}, targetDependencyID: {targetDependencyID}";
				jsonContentResult.Message = message;

				logger.Information("[{LogCategory}] " + message, "FileManagerController/UpdateDependencyID");
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				jsonContentResult.Message = "RepositoryID ?????? ?????? ?????? ??????";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/UpdateDependencyID");
				return result;
			}

			List<RepositoryItems> items = null;
			if (StaticConfig.IsLocalTransactionDB == true)
			{
				items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == sourceDependencyID && p.BusinessID == businessID);
			}
			else
			{
				items = await businessApiClient.GetRepositoryItems(repositoryID, sourceDependencyID, businessID);
			}

			bool isDataUpsert = false;
			if (items != null && items.Count > 0)
			{
				for (int i = 0; i < items.Count; i++)
				{
					RepositoryItems item = items[i];

					if (StaticConfig.IsLocalTransactionDB == true)
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
						jsonContentResult.Message = "UpdateDependencyID ????????? ?????? ??????";
						logger.Warning("[{LogCategory}] ????????? ?????? ?????? " + JsonConvert.SerializeObject(item), "FileManagerController/UpdateDependencyID");
						result = Content(JsonConvert.SerializeObject(jsonContentResult), "application/json");
						return result;
					}
				}

				jsonContentResult.Result = true;
			}
			else
			{
				jsonContentResult.Message = $"DependencyID: '{sourceDependencyID}' ?????? ?????? ?????? ?????? ??????";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/UpdateDependencyID");
			}

			result = Content(JsonConvert.SerializeObject(jsonContentResult), "application/json");

			return result;
		}

		// ItemID, FileName??? ???????????? ???????????? Profile ????????? ????????? ?????? ?????? UD02 ???????????? ?????? ????????? ????????? ?????? ??????
		private async Task<ActionResult> UpdateFileName(JsonContentResult jsonContentResult)
		{
			ActionResult result = null;
			string repositoryID = Request.Query["RepositoryID"].ToString();
			string itemID = Request.Query["ItemID"].ToString();
			string changeFileName = Request.Query["FileName"].ToString();
			string businessID = string.IsNullOrEmpty(Request.Query["BusinessID"]) == true ? "" : Request.Query["BusinessID"].ToString();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true || string.IsNullOrEmpty(changeFileName) == true)
			{
				string message = $"UPDATEFILENAME ?????? ????????? ???????????? ????????????, repositoryID: {repositoryID}, itemID: {itemID}, fileName: {changeFileName}";
				jsonContentResult.Message = message;

				logger.Information("[{LogCategory}] " + message, "FileManagerController/UpdateFileName");
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				jsonContentResult.Message = "RepositoryID ?????? ?????? ?????? ??????";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/UpdateFileName");
				return result;
			}

			RepositoryItems item = null;
			if (StaticConfig.IsLocalTransactionDB == true)
			{
				item = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.ItemID == itemID && p.BusinessID == businessID).FirstOrDefault();
			}
			else
			{
				item = await businessApiClient.GetRepositoryItem(repositoryID, itemID, businessID);
			}

			bool isDataUpsert = false;
			if (item != null)
			{
				if (item.FileName.Trim() == changeFileName.Trim())
				{
					jsonContentResult.Message = "????????? ??????????????? ?????? ??????";
					logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/UpdateFileName");
					return result;
				}

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
				repositoryManager.PersistenceDirectoryPath = repositoryManager.GetPhysicalPath(repository, businessID, customPath1, customPath2, customPath3);
				string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, businessID, customPath1, customPath2, customPath3);
				string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
				relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";
				string policyPath = repositoryManager.GetPolicyPath(repository);

				bool isExistFile = false;
				// ????????? ??????
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

								string newBlobID = relativeDirectoryUrlPath + changeFileName;
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
					jsonContentResult.Message = $"?????? ??????, ?????? ?????? ?????? ?????? ??????. repositoryID: {repositoryID}, itemID: {itemID}, fileName: {changeFileName}";
					logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/UpdateFileName");
				}

				string backupItemID = item.ItemID;
				item.ItemID = changeFileName;
				item.PhysicalPath = item.PhysicalPath.Replace(item.FileName, changeFileName);
				item.RelativePath = item.RelativePath.Replace(item.FileName, changeFileName);
				item.AbsolutePath = item.AbsolutePath.Replace(item.FileName, changeFileName);
				item.FileName = changeFileName;

				if (StaticConfig.IsLocalTransactionDB == true)
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
					jsonContentResult.Message = "UpdateDependencyID ????????? ?????? ??????";
					logger.Warning("[{LogCategory}] ????????? ?????? ?????? " + JsonConvert.SerializeObject(item), "FileManagerController/UpdateDependencyID");
					result = Content(JsonConvert.SerializeObject(jsonContentResult), "application/json");
					return result;
				}

				jsonContentResult.Result = true;
			}
			else
			{
				jsonContentResult.Message = $"????????? ??????, ?????? ?????? ?????? ?????? ??????. repositoryID: {repositoryID}, itemID: {itemID}, fileName: {changeFileName}";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/UpdateFileName");
			}

			result = Content(JsonConvert.SerializeObject(jsonContentResult), "application/json");

			return result;
		}

		private async Task<ActionResult> GetItem(JsonContentResult jsonContentResult)
		{
			ActionResult result = null;
			string repositoryID = Request.Query["RepositoryID"].ToString();
			string itemID = Request.Query["ItemID"].ToString();
			string businessID = string.IsNullOrEmpty(Request.Query["BusinessID"]) == true ? "" : Request.Query["BusinessID"].ToString();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				jsonContentResult.Message = $"GETITEM ?????? ????????? ???????????? ????????????, repositoryID: {repositoryID}, dependencyID: {itemID}";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItem");
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				jsonContentResult.Message = $"RepositoryID: '{repositoryID}' ?????? ?????? ?????? ?????? ??????";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItem");
				return result;
			}

			RepositoryItems item = null;
			if (StaticConfig.IsLocalTransactionDB == true)
			{
				item = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.ItemID == itemID && p.BusinessID == businessID).FirstOrDefault();
			}
			else
			{
				item = await businessApiClient.GetRepositoryItem(repositoryID, itemID, businessID);
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
				jsonContentResult.Message = $"ItemID: '{itemID}' ?????? ?????? ?????? ?????? ??????";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItem");
				result = Content("{}", "application/json");
			}

			return result;
		}

		private async Task<ActionResult> GetItems(JsonContentResult jsonContentResult)
		{
			ActionResult result = null;
			string repositoryID = Request.Query["RepositoryID"].ToString();
			string dependencyID = Request.Query["DependencyID"].ToString();
			string businessID = string.IsNullOrEmpty(Request.Query["BusinessID"]) == true ? "" : Request.Query["BusinessID"].ToString();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(dependencyID) == true)
			{
				jsonContentResult.Message = $"GETITEMS ?????? ????????? ???????????? ????????????, repositoryID: {repositoryID}, dependencyID: {dependencyID}";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItems");
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				jsonContentResult.Message = $"RepositoryID: '{repositoryID}' ?????? ?????? ?????? ?????? ??????";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItems");
				return result;
			}

			List<RepositoryItems> items = null;
			if (StaticConfig.IsLocalTransactionDB == true)
			{
				items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID && p.BusinessID == businessID);
			}
			else
			{
				items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID, businessID);
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
				jsonContentResult.Message = $"DependencyID: '{dependencyID}' ?????? ?????? ?????? ?????? ??????";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/GetItems");
				result = Content("{}", "application/json");
			}

			jsonContentResult.Result = true;
			result = Content(JsonConvert.SerializeObject(entitys), "application/json");

			return result;
		}

		// http://localhost:8004/api/FileManager/RepositoryRefresh
		[HttpGet("RepositoryRefresh")]
		public async Task<ContentResult> RepositoryRefresh()
		{
			string result = "true";

			try
			{
				if (StaticConfig.IsLocalTransactionDB == true)
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

		// http://localhost:8004/api/FileManager/GetRepository
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
						UploadType = repository.UploadTypeID,
						UploadExtensions = repository.UploadExtensions,
						UploadCount = repository.UploadCount,
						UploadSizeLimit = repository.UploadSizeLimit
					};
					result = JsonConvert.SerializeObject(entity);
				}
			}

			return Content(result, "application/json", Encoding.UTF8);
		}

		// http://localhost:8004/api/FileManager/UploadFile
		[HttpPost("UploadFile")]
		public async Task<ContentResult> UploadFile([FromForm] IFormFile file)
		{
			FileUploadResult result = new FileUploadResult();
			result.Result = false;
			string repositoryID = Request.Query["RepositoryID"];
			string dependencyID = Request.Query["DependencyID"];

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(dependencyID) == true)
			{
				result.Message = "RepositoryID ?????? DependencyID ?????? ?????? ?????? ??????";
				return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				result.Message = "RepositoryID ?????? ?????? ?????? ??????";
				return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
			}

			int sequence = string.IsNullOrEmpty(Request.Query["Sequence"]) == true ? 1 : Request.Query["Sequence"].ToString().GetInt();
			string saveFileName = string.IsNullOrEmpty(Request.Query["FileName"]) == true ? "" : Request.Query["FileName"].ToString();
			string itemSummary = string.IsNullOrEmpty(Request.Query["ItemSummary"]) == true ? "" : Request.Query["ItemSummary"].ToString();
			string customPath1 = string.IsNullOrEmpty(Request.Query["CustomPath1"]) == true ? "" : Request.Query["CustomPath1"].ToString();
			string customPath2 = string.IsNullOrEmpty(Request.Query["CustomPath2"]) == true ? "" : Request.Query["CustomPath2"].ToString();
			string customPath3 = string.IsNullOrEmpty(Request.Query["CustomPath3"]) == true ? "" : Request.Query["CustomPath3"].ToString();
			string userID = string.IsNullOrEmpty(Request.Query["UserID"]) == true ? "" : Request.Query["UserID"].ToString();
			string businessID = string.IsNullOrEmpty(Request.Query["BusinessID"]) == true ? "" : Request.Query["BusinessID"].ToString();

			RepositoryItems repositoryItem = null;

			if (Request.HasFormContentType == true)
			{
				if (file == null)
				{
					result.Message = "????????? ?????? ?????? ??????";
					return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
				}
				else
				{
					try
					{
						#region Form ?????????

						if (repository.UploadSizeLimit < ToFileLength(file.Length))
						{
							result.Message = repository.UploadSizeLimit.ToCurrencyString() + " ????????? ?????? ????????? ??? ??? ????????????";
							return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
						}

						RepositoryManager repositoryManager = new RepositoryManager();
						repositoryManager.PersistenceDirectoryPath = repositoryManager.GetPhysicalPath(repository, businessID, customPath1, customPath2, customPath3);
						string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, businessID, customPath1, customPath2, customPath3);
						string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
						relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";

						if (repository.IsMultiUpload.ParseBool() == true) {
							List<RepositoryItems> items = null;

							if (repository.IsFileUploadDownloadOnly.ParseBool() == true){
								result.RemainingCount = repository.UploadCount;
							}
							else
							{
								if (StaticConfig.IsLocalTransactionDB == true)
								{
									items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID && p.BusinessID == businessID);
								}
								else
								{
									items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID, businessID);
								}

								if (items != null && items.Count() > 0)
								{
									if (items.Count >= repository.UploadCount)
									{
										result.Message = repository.UploadCount.ToCurrencyString() + " ?????? ?????? ?????? ????????? ??? ??? ????????????";
										return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
									}
								}

								result.RemainingCount = repository.UploadCount - (items.Count + 1);
							}
						}
						else
						{
							List<RepositoryItems> items = null;
							if (repository.IsFileUploadDownloadOnly.ParseBool() == true)
							{
								result.RemainingCount = repository.UploadCount;
							}
							else
							{
								if (StaticConfig.IsLocalTransactionDB == true)
								{
									items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID && p.BusinessID == businessID);
								}
								else
								{
									items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID, businessID);
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

										if (StaticConfig.IsLocalTransactionDB == true)
										{
											liteDBClient.Delete<RepositoryItems>(p => p.RepositoryID == repositoryID && p.ItemID == item.ItemID && p.BusinessID == businessID);
										}
										else
										{
											await businessApiClient.DeleteRepositoryItem(repositoryID, item.ItemID, businessID);
										}
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
						repositoryItem.ItemID = (repository.IsVirtualPath.ParseBool() == false && repository.IsFileNameEncrypt.ParseBool() == true) ? Guid.NewGuid().ToString().Replace("-", string.Empty).ToUpper() : fileName;
						repositoryItem.BusinessID = businessID;
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
									relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}&BusinessID={repositoryItem.BusinessID}";
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
									relativePath = relativePath + relativeDirectoryUrlPath + repositoryItem.ItemID;
									absolutePath = "//" + Request.Host.Value + relativePath;
								}
								else
								{
									relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}&BusinessID={repositoryItem.BusinessID}";
									relativePath = Request.Path.Value.Replace("/UploadFile", "") + relativePath;
									absolutePath = "//" + Request.Host.Value + relativePath;
								}

								repositoryItem.RelativePath = relativePath;
								repositoryItem.AbsolutePath = absolutePath;

								break;
						}

						bool isDataUpsert = false;
						if (repository.IsFileUploadDownloadOnly.ParseBool() == true)
						{
							isDataUpsert = true;
						}
						else
						{
							if (StaticConfig.IsLocalTransactionDB == true)
							{
								isDataUpsert = liteDBClient.Upsert(repositoryItem);
							}
							else
							{
								isDataUpsert = await businessApiClient.UpsertRepositoryItem(repositoryItem);
							}
						}

						if (isDataUpsert == true)
						{
							result.ItemID = repositoryItem.ItemID;
							result.Result = true;
						}
						else
						{
							result.Message = "UpsertRepositoryItem ????????? ?????? ??????";
							logger.Error("[{LogCategory}] " + $"{result.Message} - {JsonConvert.SerializeObject(repositoryItem)}", "FileManagerController/UploadFile");
							return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
						}

						#endregion
					}
					catch (Exception exception)
					{
						result.Message = exception.Message;
						logger.Error("[{LogCategory}] " + $"{result.Message} - {JsonConvert.SerializeObject(repositoryItem)}", "FileManagerController/UploadFile");
						return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
					}
				}
			}
			else
			{
				string xFileName = Request.Headers["X-File-Name"];
				string xFileSize = Request.Headers["X-File-Size"];

				if (string.IsNullOrEmpty(xFileName) == true || string.IsNullOrEmpty(xFileSize) == true)
				{
					result.Message = "????????? ?????? ?????? ??????";
					return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
				}
				else
				{
					try
					{
						xFileName = WebUtility.UrlDecode(xFileName);
						string fileName = string.IsNullOrEmpty(saveFileName) == true ? xFileName : saveFileName;
						long fileLength = xFileSize.GetLong();

						#region dropzone.js ?????????

						if (repository.UploadSizeLimit < ToFileLength(fileLength))
						{
							result.Message = repository.UploadSizeLimit.ToCurrencyString() + " ????????? ?????? ????????? ??? ??? ????????????";
							return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
						}

						RepositoryManager repositoryManager = new RepositoryManager();
						repositoryManager.PersistenceDirectoryPath = repositoryManager.GetPhysicalPath(repository, businessID, customPath1, customPath2, customPath3);
						string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, businessID, customPath1, customPath2, customPath3);
						string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
						relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";

						if (repository.IsMultiUpload.ParseBool() == true)
						{
							List<RepositoryItems> items = null;
							if (StaticConfig.IsLocalTransactionDB == true)
							{
								items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID && p.BusinessID == businessID);
							}
							else
							{
								items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID, businessID);
							}

							if (items != null)
							{
								if (items.Count > repository.UploadCount)
								{
									result.Message = repository.UploadCount.ToCurrencyString() + " ?????? ?????? ?????? ????????? ??? ??? ????????????";
									return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
								}

								result.RemainingCount = repository.UploadCount - (items.Count + 1);
							}
							else {
								result.RemainingCount = repository.UploadCount;
							}
						}
						else
						{
							List<RepositoryItems> items = null;
							if (StaticConfig.IsLocalTransactionDB == true)
							{
								items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID && p.BusinessID == businessID);
							}
							else
							{
								items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID, businessID);
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

									if (StaticConfig.IsLocalTransactionDB == true)
									{
										liteDBClient.Delete<RepositoryItems>(p => p.RepositoryID == item.RepositoryID && p.ItemID == item.ItemID && p.BusinessID == businessID);
									}
									else
									{
										await businessApiClient.DeleteRepositoryItem(repositoryID, item.ItemID, businessID);
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
							extension = Path.GetExtension(xFileName);
						}

						repositoryItem = new RepositoryItems();
						repositoryItem.ItemID = (repository.IsVirtualPath.ParseBool() == false && repository.IsFileNameEncrypt.ParseBool() == true) ? Guid.NewGuid().ToString().Replace("-", string.Empty).ToUpper() : fileName;
						repositoryItem.BusinessID = businessID;
						repositoryItem.OrderBy = sequence;
						repositoryItem.ItemSummary = itemSummary;
						repositoryItem.FileName = fileName;
						repositoryItem.Extension = extension;
						repositoryItem.MimeType = GetMimeType(xFileName);
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
									relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}&BusinessID={repositoryItem.BusinessID}";
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
									relativePath = relativePath + relativeDirectoryUrlPath + repositoryItem.ItemID;
									absolutePath = "//" + Request.Host.Value + relativePath;
								}
								else
								{
									relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}&BusinessID={repositoryItem.BusinessID}";
									relativePath = Request.Path.Value.Replace("/UploadFile", "") + relativePath;
									absolutePath = "//" + Request.Host.Value + relativePath;
								}

								repositoryItem.RelativePath = relativePath;
								repositoryItem.AbsolutePath = absolutePath;
								break;
						}

						bool isDataUpsert = false;
						if (StaticConfig.IsLocalTransactionDB == true)
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
							result.Message = "UpsertRepositoryItem ????????? ?????? ??????";
							logger.Error("[{LogCategory}] " + $"{result.Message} - {JsonConvert.SerializeObject(repositoryItem)}", "FileManagerController/UploadFile");
							return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
						}

						#endregion
					}
					catch (Exception exception)
					{
						result.Message = exception.Message;
						logger.Error("[{LogCategory}] " + $"{result.Message} - {JsonConvert.SerializeObject(repositoryItem)}", "FileManagerController/UploadFile");
						return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
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

		// http://localhost:8004/api/FileManager/UploadFiles
		[HttpPost("UploadFiles")]
		public async Task<ContentResult> UploadFiles([FromForm] List<IFormFile> files)
		{
			MultiFileUploadResult result = new MultiFileUploadResult();
			result.Result = false;
			string elementID = Request.Query["ElementID"].ToString();
			string repositoryID = Request.Query["RepositoryID"].ToString();
			string dependencyID = Request.Query["DependencyID"].ToString();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(dependencyID) == true)
			{
				result.Message = "RepositoryID ?????? DependencyID ?????? ?????? ?????? ??????";
				return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				result.Message = "RepositoryID ?????? ?????? ?????? ??????";
				return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
			}

			string saveFileName = string.IsNullOrEmpty(Request.Query["FileName"]) == true ? "" : Request.Query["FileName"].ToString();
			string itemSummary = Request.Query["ItemSummary"].ToString();
			string customPath1 = Request.Query["CustomPath1"].ToString();
			string customPath2 = Request.Query["CustomPath2"].ToString();
			string customPath3 = Request.Query["CustomPath3"].ToString();
			string responseType = string.IsNullOrEmpty(Request.Query["responseType"]) == true ? "callback" : Request.Query["responseType"].ToString();
			string userID = string.IsNullOrEmpty(Request.Query["UserID"]) == true ? "" : Request.Query["UserID"].ToString();
			string callback = string.IsNullOrEmpty(Request.Query["Callback"]) == true ? "" : Request.Query["Callback"].ToString();
			string businessID = string.IsNullOrEmpty(Request.Query["BusinessID"]) == true ? "" : Request.Query["BusinessID"].ToString();

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
						stringBuilder.AppendLine("alert('????????? ????????? ?????? ??? ??? ????????????');");
						stringBuilder.AppendLine("history.go(-1);");
						stringBuilder.AppendLine(scriptEnd);
						return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
					}
					else {
						if (repository.UploadSizeLimit < ToFileLength(file.Length))
						{
							stringBuilder.AppendLine(scriptStart);
							stringBuilder.AppendLine("alert('" + repository.UploadSizeLimit.ToCurrencyString() + "?????? ????????? ??? ??? ????????????');");
							stringBuilder.AppendLine("history.go(-1);");
							stringBuilder.AppendLine(scriptEnd);
							return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
						}
					}
				}

				RepositoryManager repositoryManager = new RepositoryManager();
				repositoryManager.PersistenceDirectoryPath = repositoryManager.GetPhysicalPath(repository, businessID, customPath1, customPath2, customPath3);
				string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, businessID, customPath1, customPath2, customPath3);
				string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
				relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";
				string policyPath = repositoryManager.GetPolicyPath(repository);

				if (repository.IsMultiUpload.ParseBool() == true)
				{
					if (repository.IsFileUploadDownloadOnly.ParseBool() == true)
					{
						result.RemainingCount = repository.UploadCount;
					}
					else
					{
						List<RepositoryItems> items = null;
						if (StaticConfig.IsLocalTransactionDB == true)
						{
							items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID && p.BusinessID == businessID);
						}
						else
						{
							items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID, businessID);
						}

						if (items != null && items.Count > 0)
						{
							if ((items.Count + files.Count) > repository.UploadCount)
							{
								stringBuilder.AppendLine(scriptStart);
								stringBuilder.AppendLine("alert('" + repository.UploadCount.ToCurrencyString() + " ?????? ?????? ?????? ????????? ??? ??? ????????????');");
								stringBuilder.AppendLine("history.go(-1);");
								stringBuilder.AppendLine(scriptEnd);
								return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
							}
						}

						result.RemainingCount = repository.UploadCount - (items.Count + files.Count);
					}
				}
				else
				{
					if (repository.IsFileUploadDownloadOnly.ParseBool() == true)
					{
						result.RemainingCount = repository.UploadCount;
					}
					else
					{
						List<RepositoryItems> items = null;
						if (StaticConfig.IsLocalTransactionDB == true)
						{
							items = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID && p.BusinessID == businessID);
						}
						else
						{
							items = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID, businessID);
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

								if (StaticConfig.IsLocalTransactionDB == true)
								{
									liteDBClient.Delete<RepositoryItems>(p => p.RepositoryID == item.RepositoryID && p.ItemID == item.ItemID && p.BusinessID == businessID);
								}
								else
								{
									await businessApiClient.DeleteRepositoryItem(repositoryID, item.ItemID, businessID);
								}
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
						result.Message = "????????? ?????? ?????? ??????";
					}
					else
					{
						try
						{
							string absolutePath = "";
							string relativePath = "";
							string fileName = string.IsNullOrEmpty(saveFileName) == true ? file.FileName : saveFileName;
							string extension = Path.GetExtension(fileName);

							repositoryItem = new RepositoryItems();
							repositoryItem.ItemID = (repository.IsVirtualPath.ParseBool() == false && repository.IsFileNameEncrypt.ParseBool() == true) ? Guid.NewGuid().ToString().Replace("-", string.Empty).ToUpper() : fileName;
							repositoryItem.BusinessID = businessID;
							repositoryItem.OrderBy = sequence;
							repositoryItem.ItemSummary = itemSummary;
							repositoryItem.FileName = fileName;
							repositoryItem.Extension = extension;
							repositoryItem.MimeType = GetMimeType(fileName);
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
										relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}&BusinessID={repositoryItem.BusinessID}";
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
										relativePath = relativePath + relativeDirectoryUrlPath + repositoryItem.ItemID;
										absolutePath = "//" + Request.Host.Value + relativePath;
									}
									else
									{
										relativePath = $"/HttpDownloadFile?RepositoryID={repositoryItem.RepositoryID}&ItemID={repositoryItem.ItemID}&BusinessID={repositoryItem.BusinessID}";
										relativePath = Request.Path.Value.Replace("/UploadFiles", "") + relativePath;
										absolutePath = "//" + Request.Host.Value + relativePath;
									}

									repositoryItem.RelativePath = relativePath;
									repositoryItem.AbsolutePath = absolutePath;
									break;
							}

							bool isDataUpsert = false;
							if (repository.IsFileUploadDownloadOnly.ParseBool() == true)
							{
								isDataUpsert = true;
							}
							else
							{
								if (StaticConfig.IsLocalTransactionDB == true)
								{
									isDataUpsert = liteDBClient.Upsert(repositoryItem);
								}
								else
								{
									isDataUpsert = await businessApiClient.UpsertRepositoryItem(repositoryItem);
								}
							}

							if (isDataUpsert == true)
							{
								fileUploadResult.ItemID = repositoryItem.ItemID;
								fileUploadResult.Result = true;
							}
							else
							{
								fileUploadResult.Message = "UpsertRepositoryItem ????????? ?????? ??????";
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
				stringBuilder.AppendLine("alert('????????? ?????? ????????? ??????');");
				stringBuilder.AppendLine("history.go(-1);");
				stringBuilder.AppendLine(scriptEnd);
				return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
			}

			if (repository.IsFileUploadDownloadOnly.ParseBool() == true)
			{
				if (responseType == "callback")
				{
					stringBuilder.AppendLine(scriptStart);
					stringBuilder.AppendLine("var elementID = '" + elementID + "';");
					stringBuilder.AppendLine("var callback = '" + callback + "';");
					stringBuilder.AppendLine("var repositoryID = '" + repositoryID + "';");
					stringBuilder.AppendLine("var repositoryItems = [];");

					// stringBuilder.AppendLine("parent." + callback + "(repositoryID, repositoryItems);");
					stringBuilder.AppendLine("parent.postMessage({action: 'UploadFiles', elementID: elementID, callback: callback, repositoryID: repositoryID, repositoryItems: repositoryItems}, '*');");
					stringBuilder.AppendLine(scriptEnd);

					return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
				}
				else if (responseType == "json")
				{
					Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
					Response.Headers["Qrame_ModelType"] = "MultiFileItemResult";

					Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("[]"));
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
				List<RepositoryItems> repositoryItems = null;
				if (StaticConfig.IsLocalTransactionDB == true)
				{
					repositoryItems = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID && p.BusinessID == businessID);
				}
				else
				{
					repositoryItems = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID, businessID);
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
					stringBuilder.AppendLine("alert('????????? ?????? ????????? ??????');");
					stringBuilder.AppendLine("history.go(-1);");
					stringBuilder.AppendLine(scriptEnd);
					return Content(stringBuilder.ToString(), "text/html", Encoding.UTF8);
				}
			}
		}

		// http://localhost:8004/api/FileManager/DownloadFile
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
			string businessID = downloadRequest.BusinessID;

			// ?????? ?????? ??????

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				downloadResult.Message = "RepositoryID ?????? ItemID ?????? ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				downloadResult.Message = "RepositoryID ?????? ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			switch (repository.StorageType)
			{
				case "AzureBlob":
					result = await ExecuteBlobFileDownload(downloadResult, repositoryID, itemID, businessID);
					break;
				default:
					result = await ExecuteFileDownload(downloadResult, repositoryID, itemID, businessID);
					break;
			}

			Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
			Response.Headers["Qrame_ModelType"] = "DownloadResult";
			
			Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(downloadResult)));
			Response.Headers["X-Frame-Options"] = StaticConfig.XFrameOptions;
			return result;
		}

		// http://localhost:8004/api/FileManager/HttpDownloadFile?repositoryid=2FD91746-D77A-4EE1-880B-14AA604ACE5A&itemID=
		[HttpGet("HttpDownloadFile")]
		public async Task<ActionResult> HttpDownloadFile(string repositoryID, string itemID, string fileMD5, string tokenID, string businessID)
		{
			ActionResult result = NotFound();

			DownloadResult downloadResult = new DownloadResult();
			downloadResult.Result = false;

			// ?????? ?????? ??????

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				downloadResult.Message = "RepositoryID ?????? ItemID ?????? ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			if (string.IsNullOrEmpty(businessID) == true)
			{
				businessID = "";
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				downloadResult.Message = "RepositoryID ?????? ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			if (repository.WithOriginYN == true)
			{
				bool isWithOrigin = false;
				string requestRefererUrl = Request.Headers["Referer"];
				if (string.IsNullOrEmpty(requestRefererUrl) == false)
				{
					for (int i = 0; i < StaticConfig.WithOrigins.Count; i++)
					{
						string origin = StaticConfig.WithOrigins[i];
						if (requestRefererUrl.IndexOf(origin) > -1)
						{
							isWithOrigin = true;
							break;
						}
					}
				}

				if (isWithOrigin == false)
				{
					result = BadRequest();
					return result;
				}
			}

			switch (repository.StorageType)
			{
				case "AzureBlob":
					result = await ExecuteBlobFileDownload(downloadResult, repositoryID, itemID, businessID);
					break;
				default:
					result = await ExecuteFileDownload(downloadResult, repositoryID, itemID, businessID);
					break;
			}

			Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
			Response.Headers["Qrame_ModelType"] = "DownloadResult";
			
			Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(downloadResult)));
			Response.Headers["X-Frame-Options"] = StaticConfig.XFrameOptions;
			return result;
		}

		// http://localhost:8004/api/FileManager/VirtualDownloadFile?repositoryid=2FD91746-D77A-4EE1-880B-14AA604ACE5A&filename=?????????.jpg&subdirectory=2020
		[HttpGet("VirtualDownloadFile")]
		public async Task<ActionResult> VirtualDownloadFile(string repositoryID, string fileName, string subDirectory, string businessID)
		{
			ActionResult result = NotFound();

			DownloadResult downloadResult = new DownloadResult();
			downloadResult.Result = false;

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(fileName) == true)
			{
				downloadResult.Message = "RepositoryID ?????? fileName ?????? ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			if (string.IsNullOrEmpty(businessID) == true)
			{
				businessID = "";
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				downloadResult.Message = "RepositoryID ?????? ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			if (repository.WithOriginYN == true) {
				bool isWithOrigin = false;
				string requestRefererUrl = Request.Headers["Referer"];
				if (string.IsNullOrEmpty(requestRefererUrl) == false)
				{
					for (int i = 0; i < StaticConfig.WithOrigins.Count; i++)
					{
						string origin = StaticConfig.WithOrigins[i];
						if (requestRefererUrl.IndexOf(origin) > -1)
						{
							isWithOrigin = true;
							break;
						}
					}
				}

				if (isWithOrigin == false) {
					result = BadRequest();
					return result;
				}
			}

			result = await VirtualFileDownload(downloadResult, repositoryID, fileName, subDirectory, businessID);

			Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
			Response.Headers["Qrame_ModelType"] = "DownloadResult";
			
			Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(downloadResult)));
			Response.Headers["X-Frame-Options"] = StaticConfig.XFrameOptions;
			return result;
		}

		// http://localhost:8004/api/FileManager/VirtualDeleteFile?repositoryid=2FD91746-D77A-4EE1-880B-14AA604ACE5A&filename=?????????.jpg&subdirectory=2020
		[HttpGet("VirtualDeleteFile")]
		public async Task<ActionResult> VirtualDeleteFile(string repositoryID, string fileName, string subDirectory, string businessID)
		{
			ActionResult result = NotFound();

			DeleteResult deleteResult = new DeleteResult();
			deleteResult.Result = false;

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(fileName) == true)
			{
				deleteResult.Message = "RepositoryID ?????? fileName ?????? ?????? ?????? ??????";
				result = StatusCode(400, deleteResult.Message);
				return result;
			}

			if (string.IsNullOrEmpty(businessID) == true)
			{
				businessID = "";
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				deleteResult.Message = "RepositoryID ?????? ?????? ?????? ??????";
				result = StatusCode(400, deleteResult.Message);
				return result;
			}

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(fileName) == true)
			{
				deleteResult.Message = "RepositoryID ?????? fileName ?????? ?????? ?????? ??????";
				result = StatusCode(400, deleteResult.Message);
				return result;
			}

			if (repository.IsVirtualPath.ParseBool() == false)
			{
				deleteResult.Message = "Virtual ?????? ?????? ??????";
				result = StatusCode(400, deleteResult.Message);
				return result;
			}

			RepositoryManager repositoryManager = new RepositoryManager();

			if (repository.StorageType == "AzureBlob")
			{
				BlobContainerClient container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
				await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
				string blobID = (string.IsNullOrEmpty(businessID) == false ? businessID + "/" : "") + (string.IsNullOrEmpty(subDirectory) == false ? subDirectory + "/" : "") + fileName;
				BlobClient blob = container.GetBlobClient(blobID);
				if (await blob.ExistsAsync() == true)
				{
					Azure.Response azureResponse = await blob.DeleteAsync();
					deleteResult.Message = azureResponse.ToString();
				}
				else
				{
					result = NotFound();
					deleteResult.Message = $"????????? ?????? ??? ????????????. FileID - '{blobID}'";
				}
			}
			else
			{
				string persistenceDirectoryPath = repository.PhysicalPath;
				if (string.IsNullOrEmpty(businessID) == false)
				{
					persistenceDirectoryPath = Path.Combine(repository.PhysicalPath, businessID);
				}

				if (string.IsNullOrEmpty(subDirectory) == true)
				{
					repositoryManager.PersistenceDirectoryPath = persistenceDirectoryPath;
				}
				else
				{
					repositoryManager.PersistenceDirectoryPath = Path.Combine(persistenceDirectoryPath, subDirectory);
				}

				var filePath = Path.Combine(repositoryManager.PersistenceDirectoryPath, fileName);
				try
				{
					if (System.IO.File.Exists(filePath) == true)
					{
						System.IO.File.Delete(filePath);
						deleteResult.Result = true;
						return Content(JsonConvert.SerializeObject(result), "application/json", Encoding.UTF8);
					}
					else
					{
						result = NotFound();
						deleteResult.Message = $"????????? ?????? ??? ????????????. fileName - '{fileName}', subDirectory - '{subDirectory}'";
					}
				}
				catch (Exception exception)
				{
					result = StatusCode(500, exception.ToMessage());
					deleteResult.Message = $"????????? ?????? ??? ????????? ??????????????????. fileName - '{fileName}', subDirectory - '{subDirectory}', message - '{exception.Message}'";
					logger.Error("[{LogCategory}] " + $"{deleteResult.Message} - {exception.ToMessage()}", "FileManagerController/VirtualFileDownload");
				}
			}

			Response.Headers["Access-Control-Expose-Headers"] = "Qrame_ModelType, Qrame_Result";
			Response.Headers["Qrame_ModelType"] = "DeleteResult";

			Response.Headers["Qrame_Result"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(deleteResult)));
			Response.Headers["X-Frame-Options"] = StaticConfig.XFrameOptions;
			return result;
		}

		// http://localhost:8004/api/FileManager/GetRepositorys
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

		// http://localhost:8004/api/FileManager/RemoveItem?repositoryID=AttachFile&itemid=12345678
		[HttpGet("RemoveItem")]
		public async Task<ContentResult> RemoveItem(string repositoryID, string itemID, string businessID)
		{
			JsonContentResult jsonContentResult = new JsonContentResult();
			jsonContentResult.Result = false;

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				jsonContentResult.Message = "RepositoryID ?????? ItemID ?????? ?????? ?????? ??????";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/RemoveItem");
				return Content(JsonConvert.SerializeObject(jsonContentResult), "application/json", Encoding.UTF8);
			}

			if (string.IsNullOrEmpty(businessID) ==true) {
				businessID = "";
            }

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				jsonContentResult.Message = "RepositoryID ?????? ?????? ?????? ??????";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/RemoveItem");
				return Content(JsonConvert.SerializeObject(jsonContentResult), "application/json", Encoding.UTF8);
			}

			try
			{
				RepositoryItems repositoryItem = null;
				if (StaticConfig.IsLocalTransactionDB == true)
				{
					repositoryItem = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.ItemID == itemID && p.BusinessID == businessID).FirstOrDefault();
				}
				else
				{
					repositoryItem = await businessApiClient.GetRepositoryItem(repositoryID, itemID, businessID);
				}

				if (repositoryItem != null)
				{
					RepositoryManager repositoryManager = new RepositoryManager();
					repositoryManager.PersistenceDirectoryPath = repositoryManager.GetRepositoryItemPath(repository, repositoryItem);
					string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, repositoryItem.BusinessID, repositoryItem.CustomPath1, repositoryItem.CustomPath2, repositoryItem.CustomPath3);
					string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
					relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";

					BlobContainerClient container = null;
					bool hasContainer = false;
					if (repository.StorageType == "AzureBlob")
					{
						container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
						hasContainer = await container.ExistsAsync();
					}

					string deleteFileName;
					if (repository.IsFileNameEncrypt.ParseBool() == true)
					{
						deleteFileName = repositoryItem.ItemID;
					}
					else
					{
						deleteFileName = repositoryItem.FileName;
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

					if (StaticConfig.IsLocalTransactionDB == true)
					{
						liteDBClient.Delete<RepositoryItems>(p => p.RepositoryID == repositoryItem.RepositoryID && p.ItemID == repositoryItem.ItemID && p.BusinessID == businessID);
					}
					else
					{
						await businessApiClient.DeleteRepositoryItem(repositoryID, repositoryItem.ItemID, businessID);
					}

					jsonContentResult.Result = true;
				}
				else
				{
					jsonContentResult.Message = $"ItemID: '{itemID}' ?????? ?????? ?????? ?????? ??????";
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

		// http://localhost:8004/api/FileManager/RemoveItems?repositoryID=AttachFile&dependencyID=helloworld
		[HttpGet("RemoveItems")]
		public async Task<ContentResult> RemoveItems(string repositoryID, string dependencyID, string businessID)
		{
			JsonContentResult jsonContentResult = new JsonContentResult();
			jsonContentResult.Result = false;

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(dependencyID) == true)
			{
				jsonContentResult.Message = "RepositoryID ?????? DependencyID ?????? ?????? ?????? ??????";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/RemoveItems");
				return Content(JsonConvert.SerializeObject(jsonContentResult), "application/json", Encoding.UTF8);
			}

			if (string.IsNullOrEmpty(businessID) == true) {
				businessID = "";
            }

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);

			if (repository == null)
			{
				jsonContentResult.Message = "RepositoryID ?????? ?????? ??????";
				logger.Warning("[{LogCategory}] " + jsonContentResult.Message, "FileManagerController/RemoveItems");
				return Content(JsonConvert.SerializeObject(jsonContentResult), "application/json", Encoding.UTF8);
			}

			try
			{
				List<RepositoryItems> repositoryItems = null;
				if (StaticConfig.IsLocalTransactionDB == true)
				{
					repositoryItems = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.DependencyID == dependencyID && p.BusinessID == businessID);
				}
				else
				{
					repositoryItems = await businessApiClient.GetRepositoryItems(repositoryID, dependencyID, businessID);
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
						string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, repositoryItem.BusinessID, repositoryItem.CustomPath1, repositoryItem.CustomPath2, repositoryItem.CustomPath3);
						string relativeDirectoryUrlPath = string.IsNullOrEmpty(relativeDirectoryPath) == true ? "/" : relativeDirectoryPath.Replace(@"\", "/");
						relativeDirectoryUrlPath = relativeDirectoryUrlPath.Length <= 1 ? "" : relativeDirectoryUrlPath.Substring(relativeDirectoryUrlPath.Length - 1) == "/" ? relativeDirectoryUrlPath : relativeDirectoryUrlPath + "/";

						string deleteFileName;
						if (repository.IsFileNameEncrypt.ParseBool() == true)
						{
							deleteFileName = repositoryItem.ItemID;
						}
						else
						{
							deleteFileName = repositoryItem.FileName;
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

						if (StaticConfig.IsLocalTransactionDB == true)
						{
							liteDBClient.Delete<RepositoryItems>(p => p.RepositoryID == repositoryItem.RepositoryID && p.ItemID == repositoryItem.ItemID && p.BusinessID == businessID);
						}
						else
						{
							await businessApiClient.DeleteRepositoryItem(repositoryID, repositoryItem.ItemID, businessID);
						}
					}

					jsonContentResult.Result = true;
				}
				else
				{
					jsonContentResult.Message = $"DependencyID: '{dependencyID}' ?????? ?????? ?????? ?????? ??????";
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

		private async Task<ActionResult> VirtualFileDownload(DownloadResult downloadResult, string repositoryID, string fileName, string subDirectory, string businessID)
		{
			ActionResult result;

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(fileName) == true)
			{
				downloadResult.Message = "RepositoryID ?????? fileName ?????? ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);
			Repository repository = businessApiClient.GetRepository(repositoryID);

			if (repository == null)
			{
				downloadResult.Message = "RepositoryID ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			if (repository.IsVirtualPath.ParseBool() == false)
			{
				downloadResult.Message = "Virtual ???????????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			RepositoryManager repositoryManager = new RepositoryManager();

			if (repository.StorageType == "AzureBlob")
			{
				BlobContainerClient container = new BlobContainerClient(repository.AzureBlobConnectionString, repository.AzureBlobContainerID.ToLower());
				await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
				string blobID = (string.IsNullOrEmpty(businessID) == false ? businessID + "/" : "") + (string.IsNullOrEmpty(subDirectory) == false ? subDirectory + "/" : "") + fileName;
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
					downloadResult.Message = $"????????? ?????? ??? ????????????. FileID - '{blobID}'";
				}
			}
			else
			{
				string persistenceDirectoryPath = repository.PhysicalPath;
				if (string.IsNullOrEmpty(businessID) == false)
				{
					persistenceDirectoryPath = Path.Combine(repository.PhysicalPath, businessID);
				}

				if (string.IsNullOrEmpty(subDirectory) == true)
				{
					repositoryManager.PersistenceDirectoryPath = persistenceDirectoryPath;
				}
				else
				{
					repositoryManager.PersistenceDirectoryPath = Path.Combine(persistenceDirectoryPath, subDirectory);
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
						downloadResult.Message = $"????????? ?????? ??? ????????????. fileName - '{fileName}', subDirectory - '{subDirectory}'";
					}
				}
				catch (Exception exception)
				{
					result = StatusCode(500, exception.ToMessage());
					downloadResult.Message = $"????????? ???????????? ??? ????????? ??????????????????. fileName - '{fileName}', subDirectory - '{subDirectory}', message - '{exception.Message}'";
					logger.Error("[{LogCategory}] " + $"{downloadResult.Message} - {exception.ToMessage()}", "FileManagerController/VirtualFileDownload");
				}
			}

			return result;
		}

		private async Task<ActionResult> ExecuteBlobFileDownload(DownloadResult downloadResult, string repositoryID, string itemID, string businessID)
		{
			ActionResult result = NotFound();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				downloadResult.Message = "RepositoryID ?????? itemID ?????? ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);

			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				downloadResult.Message = "RepositoryID ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			RepositoryItems repositoryItem = null;
			if (StaticConfig.IsLocalTransactionDB == true)
			{
				repositoryItem = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.ItemID == itemID && p.BusinessID == businessID).FirstOrDefault();
			}
			else
			{
				repositoryItem = await businessApiClient.GetRepositoryItem(repositoryID, itemID, businessID);
			}

			if (repositoryItem == null)
			{
				downloadResult.Message = "?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			RepositoryManager repositoryManager = new RepositoryManager();
			string relativeDirectoryPath = repositoryManager.GetRelativePath(repository, repositoryItem.BusinessID, repositoryItem.CustomPath1, repositoryItem.CustomPath2, repositoryItem.CustomPath3);
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
				downloadResult.Message = $"????????? ?????? ??? ????????????. FileID - '{itemID}'";
			}

			return result;
		}

		private async Task<ActionResult> ExecuteFileDownload(DownloadResult downloadResult, string repositoryID, string itemID, string businessID)
		{
			ActionResult result = NotFound();

			if (string.IsNullOrEmpty(repositoryID) == true || string.IsNullOrEmpty(itemID) == true)
			{
				downloadResult.Message = "RepositoryID ?????? itemID ?????? ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			BusinessApiClient businessApiClient = new BusinessApiClient(logger);

			Repository repository = businessApiClient.GetRepository(repositoryID);
			if (repository == null)
			{
				downloadResult.Message = "RepositoryID ?????? ?????? ??????";
				result = StatusCode(400, downloadResult.Message);
				return result;
			}

			RepositoryItems repositoryItem = null;
			if (StaticConfig.IsLocalTransactionDB == true)
			{
				repositoryItem = liteDBClient.Select<RepositoryItems>(p => p.RepositoryID == repositoryID && p.ItemID == itemID && p.BusinessID == businessID).FirstOrDefault();
			}
			else
			{
				repositoryItem = await businessApiClient.GetRepositoryItem(repositoryID, itemID, businessID);
			}

			if (repositoryItem == null)
			{
				downloadResult.Message = "?????? ?????? ??????";
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
					downloadResult.Message = $"????????? ?????? ??? ????????????. FileID - '{itemID}'";
				}
			}
			catch (Exception exception)
			{
				result = StatusCode(500, exception.ToMessage());
				downloadResult.Message = $"????????? ???????????? ??? ????????? ??????????????????. FileID - '{itemID}', '{exception.Message}'";
				logger.Error("[{LogCategory}] " + $"{downloadResult.Message} - {exception.ToMessage()}", "FileManagerController/ExecuteFileDownload");
			}

			return result;
		}

		// http://localhost:8004/api/FileManager/GetMimeType?path=test.json
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

		// http://localhost:8004/api/FileManager/GetMD5Hash?value=s
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
