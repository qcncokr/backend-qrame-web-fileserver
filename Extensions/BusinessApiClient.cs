using Microsoft.Extensions.Configuration;

using Qrame.Core.Library.ApiClient;
using Qrame.Web.FileServer.Entities;

using Serilog;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Qrame.Web.FileServer.Extensions
{
    public class BusinessApiClient
	{
		private ILogger logger { get; }

		public BusinessApiClient(ILogger logger)
		{
			this.logger = logger;
		}

		public async Task<List<Repository>> GetRepositorys(string applicationIDs = "")
		{
			List<Repository> result = null;

			try
			{
				var transactionInfo = StaticConfig.TransactionFileRepositorys.Split("|");
				using (TransactionClient apiClient = new TransactionClient())
				{
					TransactionObject transactionObject = new TransactionObject();
					transactionObject.SystemID = TransactionConfig.Transaction.SystemID;
					transactionObject.ProgramID = transactionInfo[0];
					transactionObject.BusinessID = transactionInfo[1];
					transactionObject.TransactionID = transactionInfo[2];
					transactionObject.FunctionID = transactionInfo[3];
					transactionObject.ScreenID = transactionObject.TransactionID;

					List<ServiceParameter> inputs = new List<ServiceParameter>();
					inputs.Add("ApplicationID", applicationIDs);
					transactionObject.Inputs.Add(inputs);

					string requestID = "GetRepositorys" + DateTime.Now.ToString("yyyyMMddhhmmss");
					var transactionResult = await apiClient.ExecuteTransactionJson(requestID, StaticConfig.BusinessServerUrl, transactionObject);

					if (transactionResult.ContainsKey("HasException") == true)
					{
						logger.Error("[{LogCategory}] " + transactionResult["HasException"]["ErrorMessage"].ToString(), "FileManagerController/GetRepositorys");
						return result;
					}
					else
					{
						result = transactionResult["GridData0"].ToObject<List<Repository>>();
					}
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + $"applicationIDs: {applicationIDs}, Message: " + exception.ToMessage(), "BusinessApiClient/GetRepositorys");
			}

			return result;
		}

		public Repository GetRepository(string repositoryID)
		{
			Repository result = null;
			if (StaticConfig.FileRepositorys != null && StaticConfig.FileRepositorys.Count > 0)
			{
				result = StaticConfig.FileRepositorys.AsQueryable().Where(p => p.RepositoryID == repositoryID).FirstOrDefault();
			}
			return result;
		}

		public async Task<RepositoryItems> GetRepositoryItem(string repositoryID, string itemID)
		{
			RepositoryItems result = null;
			try
			{
				Repository repository = GetRepository(repositoryID);
				if (repository != null)
				{
					var transactionInfo = string.IsNullOrEmpty(repository.TransactionGetItem) == true ? "QAF|SMW|SMP030|R02".Split("|") : repository.TransactionGetItem.Split("|");
					using (TransactionClient apiClient = new TransactionClient())
					{
						TransactionObject transactionObject = new TransactionObject();
						transactionObject.SystemID = TransactionConfig.Transaction.SystemID;
						transactionObject.ProgramID = transactionInfo[0];
						transactionObject.BusinessID = transactionInfo[1];
						transactionObject.TransactionID = transactionInfo[2];
						transactionObject.FunctionID = transactionInfo[3];
						transactionObject.ScreenID = transactionObject.TransactionID;

						List<ServiceParameter> inputs = new List<ServiceParameter>();
						inputs.Add("RepositoryID", repositoryID);
						inputs.Add("ItemID", itemID);
						transactionObject.Inputs.Add(inputs);

						string requestID = "GetRepositoryItem" + DateTime.Now.ToString("yyyyMMddhhmmss");
						var transactionResult = await apiClient.ExecuteTransactionJson(requestID, StaticConfig.BusinessServerUrl, transactionObject);

						if (transactionResult.ContainsKey("HasException") == true)
						{
							logger.Error("[{LogCategory}] " + transactionResult["HasException"]["ErrorMessage"].ToString(), "FileManagerController/GetRepositoryItem");
							return result;
						}
						else
						{
							result = transactionResult["FormData0"].ToObject<RepositoryItems>();
							if (result != null && string.IsNullOrEmpty(result.ItemID) == true)
							{
								result = null;
							}
						}
					}
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + $"repositoryID: {repositoryID}, itemID: {itemID}, Message: " + exception.ToMessage(), "BusinessApiClient/GetRepositoryItem");
			}

			return result;
		}


		public async Task<List<RepositoryItems>> GetRepositoryItems(string repositoryID, string dependencyID, string itemID = "")
		{
			List<RepositoryItems> result = null;
			try
			{
				Repository repository = GetRepository(repositoryID);
				if (repository != null)
				{
					var transactionInfo = string.IsNullOrEmpty(repository.TransactionGetItems) == true ? "QAF|SMW|SMP030|R03".Split("|") : repository.TransactionGetItems.Split("|");
					using (TransactionClient apiClient = new TransactionClient())
					{
						TransactionObject transactionObject = new TransactionObject();
						transactionObject.SystemID = TransactionConfig.Transaction.SystemID;
						transactionObject.ProgramID = transactionInfo[0];
						transactionObject.BusinessID = transactionInfo[1];
						transactionObject.TransactionID = transactionInfo[2];
						transactionObject.FunctionID = transactionInfo[3];
						transactionObject.ScreenID = transactionObject.TransactionID;

						List<ServiceParameter> inputs = new List<ServiceParameter>();
						inputs.Add("RepositoryID", repositoryID);
						inputs.Add("DependencyID", dependencyID);
						inputs.Add("ItemID", itemID);
						transactionObject.Inputs.Add(inputs);

						string requestID = "GetRepositoryItems" + DateTime.Now.ToString("yyyyMMddhhmmss");
						var transactionResult = await apiClient.ExecuteTransactionJson(requestID, StaticConfig.BusinessServerUrl, transactionObject);

						if (transactionResult.ContainsKey("HasException") == true)
						{
							logger.Error("[{LogCategory}] " + transactionResult["HasException"]["ErrorMessage"].ToString(), "FileManagerController/GetRepositoryItems");
							return result;
						}
						else
						{
							result = transactionResult["GridData0"].ToObject<List<RepositoryItems>>();
						}
					}
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + $"repositoryID: {repositoryID}, dependencyID: {dependencyID}, Message: " + exception.ToMessage(), "BusinessApiClient/GetRepositoryItems");
			}

			return result;
		}

		public async Task<bool> DeleteRepositoryItem(string repositoryID, string itemID)
		{
			bool result = false;

			try
			{
				Repository repository = GetRepository(repositoryID);
				if (repository != null)
				{
					var transactionInfo = string.IsNullOrEmpty(repository.TransactionDeleteItem) == true ? "QAF|SMW|SMP030|D01".Split("|") : repository.TransactionDeleteItem.Split("|");
					using (TransactionClient apiClient = new TransactionClient())
					{
						TransactionObject transactionObject = new TransactionObject();
						transactionObject.SystemID = TransactionConfig.Transaction.SystemID;
						transactionObject.ProgramID = transactionInfo[0];
						transactionObject.BusinessID = transactionInfo[1];
						transactionObject.TransactionID = transactionInfo[2];
						transactionObject.FunctionID = transactionInfo[3];
						transactionObject.ScreenID = transactionObject.TransactionID;

						List<ServiceParameter> inputs = new List<ServiceParameter>();
						inputs.Add("RepositoryID", repositoryID);
						inputs.Add("ItemID", itemID);
						transactionObject.Inputs.Add(inputs);

						string requestID = "DeleteRepositoryItem" + DateTime.Now.ToString("yyyyMMddhhmmss");
						var transactionResult = await apiClient.ExecuteTransactionJson(requestID, StaticConfig.BusinessServerUrl, transactionObject);

						if (transactionResult.ContainsKey("HasException") == true)
						{
							logger.Error("[{LogCategory}] " + transactionResult["HasException"]["ErrorMessage"].ToString(), "FileManagerController/DeleteRepositoryItem");
							return result;
						}
						else
						{
							result = true;
						}
					}
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + $"repositoryID: {repositoryID}, itemID: {itemID}, Message: " + exception.ToMessage(), "BusinessApiClient/DeleteRepositoryItem");
			}

			return result;
		}

		public async Task<bool> UpsertRepositoryItem(RepositoryItems repositoryItem)
		{
			bool result = false;

			try
			{
				Repository repository = GetRepository(repositoryItem.RepositoryID);
				if (repository != null)
				{
					var transactionInfo = string.IsNullOrEmpty(repository.TransactionUpsertItem) == true ? "QAF|SMW|SMP030|M01".Split("|") : repository.TransactionUpsertItem.Split("|");
					using (TransactionClient apiClient = new TransactionClient())
					{
						TransactionObject transactionObject = new TransactionObject();
						transactionObject.SystemID = TransactionConfig.Transaction.SystemID;
						transactionObject.ProgramID = transactionInfo[0];
						transactionObject.BusinessID = transactionInfo[1];
						transactionObject.TransactionID = transactionInfo[2];
						transactionObject.FunctionID = transactionInfo[3];
						transactionObject.ScreenID = transactionObject.TransactionID;

						List<ServiceParameter> inputs = new List<ServiceParameter>();
						inputs.Add("ItemID", repositoryItem.ItemID);
						inputs.Add("RepositoryID", repositoryItem.RepositoryID);
						inputs.Add("DependencyID", repositoryItem.DependencyID);
						inputs.Add("FileName", repositoryItem.FileName);
						inputs.Add("OrderBy", repositoryItem.OrderBy);
						inputs.Add("ItemSummary", repositoryItem.ItemSummary);
						inputs.Add("PhysicalPath", repositoryItem.PhysicalPath);
						inputs.Add("AbsolutePath", repositoryItem.AbsolutePath);
						inputs.Add("RelativePath", repositoryItem.RelativePath);
						inputs.Add("Extension", repositoryItem.Extension);
						inputs.Add("FileLength", repositoryItem.FileLength);
						inputs.Add("MD5", repositoryItem.MD5);
						inputs.Add("MimeType", repositoryItem.MimeType);
						inputs.Add("CreationTime", repositoryItem.CreationTime.ToString("yyyy-MM-dd hh:mm:ss"));
						inputs.Add("LastWriteTime", repositoryItem.LastWriteTime.ToString("yyyy-MM-dd hh:mm:ss"));
						inputs.Add("CustomPath1", repositoryItem.CustomPath1);
						inputs.Add("CustomPath2", repositoryItem.CustomPath2);
						inputs.Add("CustomPath3", repositoryItem.CustomPath3);
						inputs.Add("PolicyPath", repositoryItem.PolicyPath);
						inputs.Add("CreateUserID", repositoryItem.CreateUserID);
						transactionObject.Inputs.Add(inputs);

						string requestID = "UpsertRepositoryItem" + DateTime.Now.ToString("yyyyMMddhhmmss");
						var transactionResult = await apiClient.ExecuteTransactionJson(requestID, StaticConfig.BusinessServerUrl, transactionObject);

						if (transactionResult.ContainsKey("HasException") == true)
						{
							logger.Error("[{LogCategory}] " + transactionResult["HasException"]["ErrorMessage"].ToString(), "FileManagerController/UpsertRepositoryItem");
							return result;
						}
						else
						{
							result = true;
						}
					}
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + $"repositoryID: {repositoryItem.RepositoryID}, itemID: {repositoryItem.ItemID}, Message: " + exception.ToMessage(), "BusinessApiClient/UpsertRepositoryItem");
			}

			return result;
		}

		public async Task<bool> UpdateDependencyID(RepositoryItems repositoryItem, string targetDependencyID)
		{
			bool result = false;

			try
			{
				Repository repository = GetRepository(repositoryItem.RepositoryID);
				if (repository != null)
				{
					var transactionInfo = string.IsNullOrEmpty(repository.TransactionUpdateDendencyID) == true ? "QAF|SMW|SMP030|U01".Split("|") : repository.TransactionUpdateDendencyID.Split("|");
					using (TransactionClient apiClient = new TransactionClient())
					{
						TransactionObject transactionObject = new TransactionObject();
						transactionObject.SystemID = TransactionConfig.Transaction.SystemID;
						transactionObject.ProgramID = transactionInfo[0];
						transactionObject.BusinessID = transactionInfo[1];
						transactionObject.TransactionID = transactionInfo[2];
						transactionObject.FunctionID = transactionInfo[3];
						transactionObject.ScreenID = transactionObject.TransactionID;

						List<ServiceParameter> inputs = new List<ServiceParameter>();
						inputs.Add("RepositoryID", repositoryItem.RepositoryID);
						inputs.Add("ItemID", repositoryItem.ItemID);
						inputs.Add("SourceDependencyID", repositoryItem.DependencyID);
						inputs.Add("TargetDependencyID", targetDependencyID);
						transactionObject.Inputs.Add(inputs);

						string requestID = "UpdateDependencyID" + DateTime.Now.ToString("yyyyMMddhhmmss");
						var transactionResult = await apiClient.ExecuteTransactionJson(requestID, StaticConfig.BusinessServerUrl, transactionObject);

						if (transactionResult.ContainsKey("HasException") == true)
						{
							logger.Error("[{LogCategory}] " + transactionResult["HasException"]["ErrorMessage"].ToString(), "FileManagerController/UpdateDependencyID");
							return result;
						}
						else
						{
							result = true;
						}
					}
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + $"repositoryID: {repositoryItem.RepositoryID}, targetDependencyID: {targetDependencyID}, Message: " + exception.ToMessage(), "BusinessApiClient/UpdateDependencyID");
			}

			return result;
		}

		public async Task<bool> UpdateFileName(RepositoryItems repositoryItem, string sourceItemID)
		{
			bool result = false;

			try
			{
				Repository repository = GetRepository(repositoryItem.RepositoryID);
				if (repository != null)
				{
					var transactionInfo = string.IsNullOrEmpty(repository.TransactionUpdateFileName) == true ? "QAF|SMW|SMP030|U02".Split("|") : repository.TransactionUpdateFileName.Split("|");
					using (TransactionClient apiClient = new TransactionClient())
					{
						TransactionObject transactionObject = new TransactionObject();
						transactionObject.SystemID = TransactionConfig.Transaction.SystemID;
						transactionObject.ProgramID = transactionInfo[0];
						transactionObject.BusinessID = transactionInfo[1];
						transactionObject.TransactionID = transactionInfo[2];
						transactionObject.FunctionID = transactionInfo[3];
						transactionObject.ScreenID = transactionObject.TransactionID;

						List<ServiceParameter> inputs = new List<ServiceParameter>();
						inputs.Add("RepositoryID", repositoryItem.RepositoryID);
						inputs.Add("SourceItemID", sourceItemID);
						inputs.Add("TargetItemID", repositoryItem.ItemID);
						inputs.Add("FileName", repositoryItem.FileName);
						transactionObject.Inputs.Add(inputs);

						string requestID = "UpdateFileName" + DateTime.Now.ToString("yyyyMMddhhmmss");
						var transactionResult = await apiClient.ExecuteTransactionJson(requestID, StaticConfig.BusinessServerUrl, transactionObject);

						if (transactionResult.ContainsKey("HasException") == true)
						{
							logger.Error("[{LogCategory}] " + transactionResult["HasException"]["ErrorMessage"].ToString(), "FileManagerController/UpdateFileName");
							return result;
						}
						else
						{
							result = true;
						}
					}
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + $"repositoryID: {repositoryItem.RepositoryID}, Message: " + exception.ToMessage(), "BusinessApiClient/UpdateFileName");
			}

			return result;
		}
	}
}
