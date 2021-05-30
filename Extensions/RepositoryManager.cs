using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

using Qrame.CoreFX.ExtensionMethod;
using Qrame.Web.FileServer.Entities;

using Serilog;

namespace Qrame.Web.FileServer.Extensions
{
    /// <summary>
    /// Repository 서비스에서 파일 저장소에 입력, 삭제등등 리소스를 관리하는 클래스입니다.
    /// </summary>
    public class RepositoryManager
    {
        private ILogger logger { get; }
        private string directoryPathFlag = "/";
        private string persistenceDirectoryPath;

        /// <summary>
        /// 파일에 대한 작업을 수행하기 위한 기본 디렉토리 경로입니다.
        /// </summary>
        public string PersistenceDirectoryPath
        {
            get { return this.persistenceDirectoryPath; }
            set { this.persistenceDirectoryPath = value; }
        }

        /// <summary>
        /// 기본 생성자입니다.
        /// </summary>
        public RepositoryManager()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == true)
            {
                directoryPathFlag = @"\";
            }
            else
            {
                directoryPathFlag = "/";
            }
        }

        /// <summary>
        /// 저장 경로를 포함하는 기본 생성자입니다.
        /// </summary>
        /// <param name="persistenceDirectoryPath">기본 저장경로입니다.</param>
        public RepositoryManager(ILogger logger) : base()
        {
            this.logger = logger;
        }

        /// <summary>
        /// 저장소 디렉토리 경로를 반환합니다.
        /// </summary>
        /// <param name="repository">Repository 저장소를 표현하는 객체입니다.</param>
        /// <returns>동적으로 생성되는 디렉토리입니다.</returns>
        public string GetPolicyPath(Repository repository)
        {
            string result = "";
            if (repository.IsAutoPath.ParseBool() == true)
            {
                switch (repository.PolicyPathID)
                {
                    case "1":
                        result = DateTime.Now.ToString("yyyy");
                        break;
                    case "2":
                        result = DateTime.Now.ToString("yyyy-MM");
                        break;
                    case "3":
                        result = DateTime.Now.ToString("yyyy-MM-dd");
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// 저장소 디렉토리 절대경로를 반환합니다.
        /// </summary>
        /// <param name="repository">Repository 저장소를 표현하는 객체입니다.</param>
        /// <param name="customPath1">첫번째 우선순위인 추가 식별자입니다.</param>
        /// <param name="customPath2">두번째 우선순위인 추가 식별자입니다.</param>
        /// <param name="customPath3">세번째 우선순위인 추가 식별자입니다.</param>
        /// <returns></returns>
        public string GetPhysicalPath(Repository repository, string customPath1 = "", string customPath2 = "", string customPath3 = "")
        {
            string result = "";
            if (repository.IsAutoPath.ParseBool() == true)
            {
                string dynamicPath = "";
                switch (repository.PolicyPathID)
                {
                    case "1": // 참조식별자+년도
                        dynamicPath = GetCustomFileStoragePath(customPath1, customPath2, customPath3) + DateTime.Now.ToString("yyyy") + directoryPathFlag;
                        break;
                    case "2": // 참조식별자+년월
                        dynamicPath = GetCustomFileStoragePath(customPath1, customPath2, customPath3) + DateTime.Now.ToString("yyyy-MM") + directoryPathFlag;
                        break;
                    case "3": // 참조식별자+년월일
                        dynamicPath = GetCustomFileStoragePath(customPath1, customPath2, customPath3) + DateTime.Now.ToString("yyyy-MM-dd") + directoryPathFlag;
                        break;
                    default:
                        dynamicPath = GetCustomFileStoragePath(customPath1, customPath2, customPath3);
                        break;
                }
                result = Path.Combine(repository.PhysicalPath, dynamicPath);
            }
            else
            {
                result = Path.Combine(repository.PhysicalPath, GetCustomFileStoragePath(customPath1, customPath2, customPath3));
            }

            return result;
        }

        /// <summary>
        /// 저장소 디렉토리 절대경로를 반환합니다.
        /// </summary>
        /// <param name="repository">Repository 저장소를 표현하는 객체입니다.</param>
        /// <param name="customPath1">첫번째 우선순위인 추가 식별자입니다.</param>
        /// <param name="customPath2">두번째 우선순위인 추가 식별자입니다.</param>
        /// <param name="customPath3">세번째 우선순위인 추가 식별자입니다.</param>
        /// <returns></returns>
        public string GetRelativePath(Repository repository, string customPath1 = "", string customPath2 = "", string customPath3 = "")
        {
            string result;
            if (repository.IsAutoPath.ParseBool() == true)
            {
                string dynamicPath;
                switch (repository.PolicyPathID)
                {
                    case "1": // 참조식별자+년도
                        dynamicPath = GetCustomFileStoragePath(customPath1, customPath2, customPath3) + DateTime.Now.ToString("yyyy") + directoryPathFlag;
                        break;
                    case "2": // 참조식별자+년월
                        dynamicPath = GetCustomFileStoragePath(customPath1, customPath2, customPath3) + DateTime.Now.ToString("yyyy-MM") + directoryPathFlag;
                        break;
                    case "3": // 참조식별자+년월일
                        dynamicPath = GetCustomFileStoragePath(customPath1, customPath2, customPath3) + DateTime.Now.ToString("yyyy-MM-dd") + directoryPathFlag;
                        break;
                    default:
                        dynamicPath = GetCustomFileStoragePath(customPath1, customPath2, customPath3);
                        break;
                }
                result = dynamicPath;
            }
            else
            {
                result = GetCustomFileStoragePath(customPath1, customPath2, customPath3);
            }

            return result;
        }

        /// <summary>
        /// 아이템 저장소 디렉토리 경로를 반환합니다.
        /// </summary>
        /// <param name="repository">Repository 저장소를 표현하는 객체입니다.</param>
        /// <param name="custom1">첫번째 우선순위인 추가 식별자입니다.</param>
        /// <param name="custom2">두번째 우선순위인 추가 식별자입니다.</param>
        /// <param name="custom3">세번째 우선순위인 추가 식별자입니다.</param>
        /// <param name="repositoryItem">RepositoryItemsObject 입니다.</param>
        /// <returns></returns>
        public string GetRepositoryItemPath(Repository repository, RepositoryItems repositoryItem)
        {
            string result = "";

            if (repository.StorageType == "AzureBlob")
            {
                if (repository.IsAutoPath.ParseBool() == true)
                {
                    result = GetCustomUrlPath(repositoryItem.CustomPath1, repositoryItem.CustomPath2, repositoryItem.CustomPath3) + repositoryItem.PolicyPath;
                }
                else
                {
                    result = GetCustomUrlPath(repositoryItem.CustomPath1, repositoryItem.CustomPath2, repositoryItem.CustomPath3);
                }
            }
            else
            {
                if (repository.IsAutoPath.ParseBool() == true)
                {
                    string dynamicPath = GetCustomFileStoragePath(repositoryItem.CustomPath1, repositoryItem.CustomPath2, repositoryItem.CustomPath3) + repositoryItem.PolicyPath;
                    result = Path.Combine(repository.PhysicalPath, dynamicPath);
                }
                else
                {
                    result = Path.Combine(repository.PhysicalPath, GetCustomFileStoragePath(repositoryItem.CustomPath1, repositoryItem.CustomPath2, repositoryItem.CustomPath3));
                }
            }

            return result;
        }

        private Uri GetServiceSasUriForContainer(BlobContainerClient blobContainerClient, DateTimeOffset expiresOn, string storedPolicyName = null)
        {
            if (blobContainerClient.CanGenerateSasUri)
            {
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = blobContainerClient.Name,
                    Resource = "c"
                };

                if (storedPolicyName == null)
                {
                    sasBuilder.ExpiresOn = expiresOn == null ? DateTimeOffset.UtcNow.AddHours(1) : expiresOn;
                    sasBuilder.SetPermissions(BlobSasPermissions.Read);
                }
                else
                {
                    sasBuilder.Identifier = storedPolicyName;
                }

                Uri sasUri = blobContainerClient.GenerateSasUri(sasBuilder);
                return sasUri;
            }
            else
            {
                logger.Warning("[{LogCategory}] 서비스 SAS를 생성하려면 BlobContainerClient 공유 키 자격 증명이 있어야합니다", "RepositoryManager/GetServiceSasUriForContainer");
                return null;
            }
        }

        private Uri GetServiceSasUriForBlob(BlobClient blobClient, DateTimeOffset expiresOn, string storedPolicyName = null)
        {
            if (blobClient.CanGenerateSasUri)
            {
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = blobClient.GetParentBlobContainerClient().Name,
                    BlobName = blobClient.Name,
                    Resource = "b"
                };

                if (storedPolicyName == null)
                {
                    sasBuilder.ExpiresOn = expiresOn == null ? DateTimeOffset.UtcNow.AddHours(1) : expiresOn;
                    sasBuilder.SetPermissions(BlobSasPermissions.Read);
                }
                else
                {
                    sasBuilder.Identifier = storedPolicyName;
                }

                Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
                return sasUri;
            }
            else
            {
                logger.Warning("[{LogCategory}] 서비스 SAS를 생성하려면 BlobClient에 공유 키 자격 증명이 있어야합니다", "RepositoryManager/GetServiceSasUriForBlob");
                return null;
            }
        }

        /// <summary>
        /// 참조식별자 데이터로 되어있는 Url 디렉토리 경로를 반환합니다.
        /// </summary>
        /// <param name="customPath1">첫번째 우선순위인 추가 식별자입니다.</param>
        /// <param name="customPath2">두번째 우선순위인 추가 식별자입니다.</param>
        /// <param name="customPath3">세번째 우선순위인 추가 식별자입니다.</param>
        /// <returns>디렉토리 경로를 반환합니다.</returns>
        public string GetCustomUrlPath(string customPath1, string customPath2, string customPath3)
        {
            string result = "";

            if (string.IsNullOrEmpty(customPath1) == false)
            {
                result += customPath1 + "/";
            }

            if (string.IsNullOrEmpty(customPath2) == false)
            {
                result += customPath2 + "/";
            }

            if (string.IsNullOrEmpty(customPath3) == false)
            {
                result += customPath3 + "/";
            }

            return result;
        }

        /// <summary>
        /// 참조식별자 데이터로 되어있는 디렉토리 경로를 반환합니다.
        /// </summary>
        /// <param name="customPath1">첫번째 우선순위인 추가 식별자입니다.</param>
        /// <param name="customPath2">두번째 우선순위인 추가 식별자입니다.</param>
        /// <param name="customPath3">세번째 우선순위인 추가 식별자입니다.</param>
        /// <returns>디렉토리 경로를 반환합니다.</returns>
        public string GetCustomFileStoragePath(string customPath1, string customPath2, string customPath3)
        {
            string result = "";

            if (string.IsNullOrEmpty(customPath1) == false)
            {
                result += customPath1 + directoryPathFlag;
            }

            if (string.IsNullOrEmpty(customPath2) == false)
            {
                result += customPath2 + directoryPathFlag;
            }

            if (string.IsNullOrEmpty(customPath3) == false)
            {
                result += customPath3 + directoryPathFlag;
            }

            return result;
        }

        /// <summary>
        /// 지정된 파일명으로 문자열 내용을 저장합니다. 대상 파일이 이미 있으면 덮어버립니다.
        /// </summary>
        /// <param name="fileName">경로를 포함하는 텍스트 파일명입니다.</param>
        /// <param name="addedText">텍스트 파일의 내용 문자열입니다.</param>
        public void WriteTextFile(string fileName, string addedText)
        {
            File.WriteAllText(Path.Combine(persistenceDirectoryPath, fileName), addedText);
        }

        /// <summary>
        /// StorageType이 AzureBlob일 경우 지정한 파일이 업로드 대상 디렉토리에 존재하는지 확인하여 파일명을 반환합니다. 중복된 파일명이 있을 경우 순번을 붙여 반환합니다.
        /// </summary>
        /// <param name="blobID">업로드한 파일에 대한 식별자입니다.</param>
        public async Task<string> GetDuplicateCheckUniqueFileName(BlobContainerClient container, string blobID)
        {
            string result;
            BlobClient blob = container.GetBlobClient(blobID);
            if (await blob.ExistsAsync() == true)
            {
                string originalBlobID = blobID;
                if (File.Exists(Path.Combine(this.PersistenceDirectoryPath, blobID)) == false)
                {
                    result = blobID;
                }
                else
                {
                    int i = 0;
                    string extension = Path.GetExtension(blobID);
                    blobID = blobID.Replace(extension, "");
                    do
                    {
                        blobID = string.Concat(originalBlobID, " (", (i++).ToString(), ")", extension);
                        blob = container.GetBlobClient(blobID);
                    } while (await blob.ExistsAsync());

                    result = blobID;
                }
            }
            else {
                result = blobID;
            }

            return result;
        }

        /// <summary>
        /// StorageType이 FileSystem일 경우 지정한 파일이 업로드 대상 디렉토리에 존재하는지 확인하여 파일명을 반환합니다. 중복된 파일명이 있을 경우 순번을 붙여 반환합니다.
        /// </summary>
        /// <param name="fileName">업로드한 파일에 대한 식별자입니다.</param>
        public string GetDuplicateCheckUniqueFileName(string fileName)
        {
            string result;
            if (File.Exists(Path.Combine(this.PersistenceDirectoryPath, fileName)) == true)
            {
                string originalFileName = fileName;
                int i = 0;
                string extension = Path.GetExtension(fileName);
                fileName = fileName.Replace(extension, "");
                FileInfo fileInfo;
                do
                {
                    fileName = string.Concat(originalFileName, " (", (i++).ToString(), ")", extension);
                    fileInfo = new FileInfo(Path.Combine(this.PersistenceDirectoryPath, fileName));
                } while (fileInfo.Exists);

                result = fileName;
            }
            else
            {
                result = fileName;
            }

            return result;
        }

        /// <summary>
        /// 파일을 저장할 실제 경로를 반환합니다
        /// </summary>
        /// <param name="postedFile">클라이언트에서 업로드한 개별 파일입니다.</param>
        /// <param name="itemID">업로드한 파일에 대한 식별자입니다.</param>
        public string GetSavePath(string itemID)
        {
            DirectoryInfo saveFolder = new DirectoryInfo(this.PersistenceDirectoryPath);

            if (saveFolder.Exists == false)
            {
                saveFolder.Create();
            }

            return Path.Combine(this.PersistenceDirectoryPath, itemID);
        }

        /// <summary>
        /// 업로드된 개별 파일을 이동합니다.
        /// </summary>
        /// <param name="sourceFileName">업로드한 파일에 대한 식별자입니다.</param>
        /// <param name="sourceFileName">파일에 대한 새 경로입니다.</param>
        public void Move(string sourceFileName, string destnationFileName)
        {
            if (string.IsNullOrEmpty(sourceFileName) == false)
            {
                string fileName = GetSavePath(sourceFileName);
                if (File.Exists(fileName) == true)
                {
                    File.Move(fileName, destnationFileName, true);
                }
            }
        }

        /// <summary>
        /// 업로드된 개별 파일을 삭제합니다.
        /// </summary>
        /// <param name="itemID">업로드한 파일에 대한 식별자입니다.</param>
        public void Delete(string itemID)
        {
            if (string.IsNullOrEmpty(itemID) == false)
            {
                string fileName = Path.Combine(this.PersistenceDirectoryPath, itemID);
                if (File.Exists(fileName) == true)
                {
                    File.Delete(fileName);
                }
            }
        }

        /// <summary>
        /// 섬네일 이미지를 생성합니다. 1MByte이상 대용량 이미지 파일인 경우 사용하세요.
        /// </summary>
        /// <param name="imageFilePath">이미지 파일 경로입니다.</param>
        /// <param name="keepOriginalSizeRatio">사이즈 변경시 원본 사이즈 비율을 유지할지 결정합니다.</param>
        /// <param name="thumbnailMaxWidth">이미지 사이즈의 최대 Width 값입니다.</param>
        /// <param name="thumbnailMaxHeight">이미지 사이즈의 최대 Height 값입니다.</param>
        /// <returns>Bitmap 및 Metafile 기능을 제공하는 객체를 반환합니다.</returns>
        public Image CreateThumbnailImage(string imageFilePath, bool keepOriginalSizeRatio, int thumbnailMaxWidth, int thumbnailMaxHeight)
        {
            int thumbnailWidth = 0;
            int thumbnailHeight = 0;
            Image thumbnailImage = null;
            if (File.Exists(imageFilePath) == true)
            {
                using (Bitmap originalBitMap = new Bitmap(imageFilePath))
                {
                    getThumbnailSize(originalBitMap.Width, originalBitMap.Height, keepOriginalSizeRatio, thumbnailMaxWidth, thumbnailMaxHeight, out thumbnailWidth, out thumbnailHeight);

                    thumbnailImage = originalBitMap.GetThumbnailImage(thumbnailWidth, thumbnailHeight, null, IntPtr.Zero);
                }
            }
            return thumbnailImage;
        }

        /// <summary>
        /// 섬네일 이미지를 생성합니다. 1MByte이상 대용량 이미지 파일인 경우 사용하세요.
        /// </summary>
        /// <param name="imageFilePath">이미지 파일 경로입니다.</param>
        /// <param name="keepOriginalSizeRatio">사이즈 변경시 원본 사이즈 비율을 유지할지 결정합니다.</param>
        /// <param name="thumbnailMaxWidth">이미지 사이즈의 최대 Width 값입니다.</param>
        /// <param name="thumbnailMaxHeight">이미지 사이즈의 최대 Height 값입니다.</param>
        /// <returns>이미지 파일 경로를 반환합니다.</returns>
        public string CreateThumbnailImageFile(string imageFilePath, bool keepOriginalSizeRatio, int thumbnailMaxWidth, int thumbnailMaxHeight)
        {
            int thumbnailWidth = 0;
            int thumbnailHeight = 0;
            string saveImageFilePath = string.Empty;

            if (File.Exists(imageFilePath) == true)
            {
                using (Bitmap originalBitMap = new Bitmap(imageFilePath))
                {
                    getThumbnailSize(originalBitMap.Width, originalBitMap.Height, keepOriginalSizeRatio, thumbnailMaxWidth, thumbnailMaxHeight, out thumbnailWidth, out thumbnailHeight);
                }

                using (Image sourceImage = Image.FromFile(imageFilePath))
                {
                    using (Bitmap bitmap = new Bitmap(sourceImage, new Size(thumbnailWidth, thumbnailHeight)))
                    {
                        saveImageFilePath = imageFilePath;
                        saveImageFilePath = saveImageFilePath + ".t." + ImageFormat.Jpeg.ToString();
                        bitmap.Save(saveImageFilePath, ImageFormat.Jpeg);
                    }
                }
            }
            return saveImageFilePath;
        }

        /// <summary>
        /// 섬네일 이미지 사이즈를 계산합니다.
        /// </summary>
        /// <param name="originalWidth">원본 이미지 Width값입니다.</param>
        /// <param name="originalHeight">원본 이미지 Height값입니다.</param>
        /// <param name="keepOriginalSizeRatio">사이즈 변경시 원본 사이즈 비율을 유지할지 결정합니다.</param>
        /// <param name="thumbnailMaxWidth">섬네일 이미지 최대 Width값입니다.</param>
        /// <param name="thumbnailMaxHeight">섬네일 이미지 최대 Height값입니다.</param>
        /// <param name="thumbnailWidth">섬네일 이미지 Width값입니다.</param>
        /// <param name="thumbnailHeight">섬네일 이미지 Height값입니다.</param>
        private void getThumbnailSize(int originalWidth, int originalHeight, bool keepOriginalSizeRatio, int thumbnailMaxWidth, int thumbnailMaxHeight, out int thumbnailWidth, out int thumbnailHeight)
        {
            if (keepOriginalSizeRatio)
            {
                thumbnailWidth = originalWidth;
                thumbnailHeight = originalHeight;
                Single ratio = (((Single)originalHeight) / ((Single)originalWidth)) * 100f;
                if (ratio < 100f)
                {
                    if (originalWidth > thumbnailMaxWidth)
                    {
                        thumbnailWidth = thumbnailMaxWidth;
                        thumbnailHeight = (originalHeight * thumbnailMaxWidth) / originalWidth;
                    }
                }
                else if (originalHeight > thumbnailMaxHeight)
                {
                    thumbnailWidth = (originalWidth * thumbnailMaxHeight) / originalHeight;
                    thumbnailHeight = thumbnailMaxHeight;
                }
            }
            else
            {
                thumbnailWidth = thumbnailMaxWidth;
                thumbnailHeight = thumbnailMaxHeight;
            }
        }
    }
}
