using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using static Google.Apis.Requests.BatchRequest;
namespace FileManager
{
    internal class GoogleDriveMenuFunctions
    {
        private readonly DriveService _driveService;
        private string _cutOrCopyItemId; // ID елемента, який потрібно вставити
        private bool _isCutOperation; // Прапор: true, якщо операція – вирізання

        public GoogleDriveMenuFunctions(DriveService driveService)
        {
            _driveService = driveService;
        }
    
        public async Task CreateItem(string itemType, string parentId, string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(parentId))
                {
                    throw new ArgumentException("ID батьківської папки не може бути порожнім.", nameof(parentId));
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Ім'я елемента не може бути порожнім.", nameof(name));
                }
                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = name,
                    Parents = new List<string> { parentId }
                };

                // Визначаємо MIME-тип залежно від типу елемента
                switch (itemType)
                {
                    //ПЕРЕВІРКА НА ІСНУВАННЯ ЩОБ писати Нова папка(1)
                    case "Folder":
                        
                        fileMetadata.MimeType = "application/vnd.google-apps.folder";
                        break;
                    case "TextFile":   // Додавання підтримки текстового файлу

                        fileMetadata.MimeType = "text/plain";
                        break;
                    case "GoogleDoc":
                        fileMetadata.MimeType = "application/vnd.google-apps.document";
                        break;
                    case "GoogleSheet":
                        fileMetadata.MimeType = "application/vnd.google-apps.spreadsheet";
                        break;
                    case "GoogleSlide":
                        fileMetadata.MimeType = "application/vnd.google-apps.presentation";
                        break;
                    default:
                        throw new Exception("Невідомий тип елементу.");
                }

                // Створюємо елемент
                var request = _driveService.Files.Create(fileMetadata);
                request.Fields = "id";
                //Thread.Sleep(1500);
                var file = await request.ExecuteAsync();
                //await Task.Delay(500);
                
                
            }
            catch (Exception ex)
            {
                throw new Exception($"Помилка при створенні елементу: {ex.Message}");
            }
        }
  
        public async Task RenameItem(string itemId, string newName)
        {
            try
            {
                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = newName
                };

                var updateRequest = _driveService.Files.Update(fileMetadata, itemId);
                updateRequest.Fields = "id, name";
                await updateRequest.ExecuteAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Помилка при перейменуванні елементу: {ex.Message}");
            }
        }


        public void CopyItem(string itemId)
        {
            _cutOrCopyItemId = itemId;
            _isCutOperation = false;
        }

        
        public void CutItem(string itemId)
        {
            _cutOrCopyItemId = itemId;
            _isCutOperation = true;
        }

        public async Task PasteItem(string destinationFolderId)
        {
            try
            {

                // Отримуємо інформацію про елемент, щоб визначити, чи він папкою
                var getRequest = _driveService.Files.Get(_cutOrCopyItemId);
                getRequest.Fields = "id, name, mimeType";
                var item = await getRequest.ExecuteAsync();

                bool isFolder = item.MimeType == "application/vnd.google-apps.folder";

                if (_isCutOperation)
                {

                    // Вирізання
                    if (isFolder)
                    {

                        // Вирізати папку
                        await MoveFolder(_cutOrCopyItemId, destinationFolderId);
                    }
                    else
                    {
                        // Вирізати файл
                        await MoveItem(_cutOrCopyItemId, destinationFolderId);
                    }
                }
                else
                {
                    // Копіювання
                    if (isFolder)
                    {
                        // Копіювати папку
                        await CopyFolder(_cutOrCopyItemId, destinationFolderId);
                    }
                    else
                    {
                        // Копіювати файл
                        await CopyItemToFolder(_cutOrCopyItemId, destinationFolderId);
                    }
                }

                // Скидаємо ID після виконання операції
                _cutOrCopyItemId = null;
                _isCutOperation = false;

               
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при всталенні елементу: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Помилка при всталенні елементу: {ex.Message}");
            }
        }


        private async Task MoveItem(string itemId, string newParentId)
        {
            try
            {
                // Отримуємо поточні батьківські папки
                var getRequest = _driveService.Files.Get(itemId);
                getRequest.Fields = "parents"; // Вказуємо, що хочемо отримати список батьків
                var file = await getRequest.ExecuteAsync();


                // Видаляємо з поточних батьків і додаємо до нової папки
                var updateRequest = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), itemId);
                updateRequest.AddParents = newParentId;
                updateRequest.RemoveParents = string.Join(",", file.Parents);
                updateRequest.Fields = "id, parents"; // Вказуємо, що хочемо оновити батьківські папки

                var result = await updateRequest.ExecuteAsync();

                //MessageBox.Show($"Файл успішно переміщено! Новий батьківський ID: {newParentId}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Помилка при переміщенні елементу: {ex.Message}");
            }
        }

        private async Task CopyItemToFolder(string itemId, string parentId, string newFileName = null)
        {
            try
            {    //MessageBox.Show($"Копіювання: itemId={itemId}, parentId={parentId}");
                var getRequest = _driveService.Files.Get(itemId);
                getRequest.Fields = "name";  // Запитуємо лише ім'я файлу

                var file = await getRequest.ExecuteAsync();

                // Якщо нове ім'я не вказано, використовуємо оригінальне ім'я файлу
                string fileName = newFileName ?? file.Name;


                // Налаштування метаданих для нового файлу
                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = fileName,// Унікальне ім'я або задане
                    Parents = new List<string> { parentId }
                };

                // Создание запроса на копирование
                var copyRequest = _driveService.Files.Copy(fileMetadata, itemId);
                copyRequest.Fields = "id, name, parents"; // Поля, которые вы хотите получить в ответе
                copyRequest.SupportsAllDrives = true; // Для работы с Shared Drives


                // Виконання запиту
                var result = await copyRequest.ExecuteAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Помилка при копіювання елементу: {ex.Message}");
            }
        }
        private async Task CopyFolder(string folderId, string newParentId, string newFolderName = null)
        {
            try
            {
                // Отримуємо метадані оригінальної папки
                var getRequest = _driveService.Files.Get(folderId);
                getRequest.Fields = "name";
                var folder = await getRequest.ExecuteAsync();

                // Створюємо нову папку
                var newFolderMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = newFolderName ?? folder.Name,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = new List<string> { newParentId }
                };
                var createRequest = _driveService.Files.Create(newFolderMetadata);
                createRequest.Fields = "id";
                var newFolder = await createRequest.ExecuteAsync();

                // Отримання вмісту папки
                var listRequest = _driveService.Files.List();
                listRequest.Q = $"'{folderId}' in parents";
                listRequest.Fields = "files(id, mimeType)";
                var contents = await listRequest.ExecuteAsync();

                // Рекурсивно копіюємо вміст
                foreach (var item in contents.Files)
                {
                    if (item.MimeType == "application/vnd.google-apps.folder")
                    {
                        await CopyFolder(item.Id, newFolder.Id);// Рекурсивно копіюємо підпапки
                    }
                    else
                    {
                        await CopyItemToFolder(item.Id, newFolder.Id); // Копіюємо файли
                    }
                }

               // MessageBox.Show($"Папка {folder.Name} успішно скопійованаа.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Помилка при копіюванні папки: {ex.Message}");
            }
        }
        private async Task MoveFolder(string folderId, string newParentId)
        {
            try
            {

                // Переміщаємо саму папку
                var getRequest = _driveService.Files.Get(folderId);
                getRequest.Fields = "parents";
                var folder = await getRequest.ExecuteAsync();

                var updateRequest = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), folderId);
                updateRequest.AddParents = newParentId;
                updateRequest.RemoveParents = string.Join(",", folder.Parents);
                updateRequest.Fields = "id, parents";
                await updateRequest.ExecuteAsync();

                // Отримання вмісту папки
                var listRequest = _driveService.Files.List();
                listRequest.Q = $"'{folderId}' in parents";
                listRequest.Fields = "files(id, mimeType)";
                var contents = await listRequest.ExecuteAsync();

                // Рекурсивно переміщуємо вміст
                foreach (var item in contents.Files)
                {
                    if (item.MimeType == "application/vnd.google-apps.folder")
                    {
                        await MoveFolder(item.Id, folderId);// Рекурсивно переміщаємо підпапки
                    }
                    else
                    {
                        await MoveItem(item.Id, folderId); // Переміщуємо файли
                    }
                }

                //MessageBox.Show($"Папка {folderId} успішно переміщена.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Помилка при переміщенні папки: {ex.Message}");
            }
        }
        
        public async Task<Google.Apis.Drive.v3.Data.File> GetItemInfo(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new Exception("ID елемента не може бути порожнім.");
            }

            try
            {
                var getRequest = _driveService.Files.Get(itemId);


                // Вказуємо необхідні поля
                getRequest.Fields = "id, name, mimeType";

                var file = await getRequest.ExecuteAsync();
                return file;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при отриманні інформації про елемент: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Помилка при отриманні інформації про елемент: {ex.Message}");
            }
        }
        public async Task DeleteItem(string itemId)
        {
            try
            {
                // Створення запиту на видалення
                var deleteRequest = _driveService.Files.Delete(itemId);
                await deleteRequest.ExecuteAsync();

                // MessageBox.Show($"Элемент с ID {itemId} успішно видалено.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при видаленні елемента: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Помилка при видаленні елемента: {ex.Message}");
            }
        }
        public async Task DeleteItemWithConfirmation(string itemId)
        {
            try
            {
                // Отримання інформації про елемент
                var fileInfo = await GetItemInfo(itemId);
                // Підтвердження видалення
                var result = MessageBox.Show(
                    $"Ви впевнені, що хочете видалити \"{fileInfo.Name}\"?",
                    "Підтвердження видалення",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.Yes)
                {
                    await DeleteItem(itemId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при видаленні елемента: {ex.Message}");
            }
        }
        private string GetMimeType(string fileName)
        {
            // Отримуємо розширення файлу
            string extension = Path.GetExtension(fileName).ToLowerInvariant();

            // Список відомих MIME-типів
            var mimeTypes = new Dictionary<string, string>
         {
            { ".txt", "text/plain" },
            { ".pdf", "application/pdf" },
            { ".doc", "application/msword" },
            { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            { ".xls", "application/vnd.ms-excel" },
            { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".csv", "text/csv" },
            { ".zip", "application/zip" },
            { ".rar", "application/x-rar-compressed" },
            { ".mp4", "video/mp4" },
            { ".mp3", "audio/mpeg" },
            { ".wav", "audio/wav" },
            { ".html", "text/html" },
            { ".xml", "application/xml" },
            { ".json", "application/json" }
        };


            // Повертаємо відповідний MIME-тип або значення за замовчуванням
            return mimeTypes.TryGetValue(extension, out string mimeType) ? mimeType : "application/octet-stream";
        }
        public async Task UploadFileToGoogleDrive(string localFilePath, string googleDriveFolderId)
        {
            var fileMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = Path.GetFileName(localFilePath),
                Parents = new List<string> { googleDriveFolderId }
            };
            //MessageBox.Show("filestream");
            using (var stream = new FileStream(localFilePath, FileMode.Open))
            {   
                var request = _driveService.Files.Create(fileMetadata, stream, GetMimeType(localFilePath));
               // MessageBox.Show("filestream2");
                await request.UploadAsync();
            }
        }
        public async Task UploadFolderToGoogleDrive(string localFolderPath, string googleDriveParentFolderId)
        {

            // Отримання імені папки
            string folderName = Path.GetFileName(localFolderPath);

            // Створюємо папку на Google Drive
            var folderMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder",
                Parents = new List<string> { googleDriveParentFolderId }
            };

            var createFolderRequest = _driveService.Files.Create(folderMetadata);
            var createdFolder = await createFolderRequest.ExecuteAsync();

            string newGoogleDriveFolderId = createdFolder.Id;

            // Завантажуємо всі файли та папки всередині поточної папки
            foreach (var filePath in Directory.GetFiles(localFolderPath))
            {
                await UploadFileToGoogleDrive(filePath, newGoogleDriveFolderId);
            }

            foreach (var subFolderPath in Directory.GetDirectories(localFolderPath))
            {
                await UploadFolderToGoogleDrive(subFolderPath, newGoogleDriveFolderId);
            }
        }
        public async Task DownloadFileFromGoogleDrive(string googleDriveFileId, string localFilePath)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(localFilePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var request = _driveService.Files.Get(googleDriveFileId);
                request.Fields = "id, name, mimeType";
                var fileInfo = await request.ExecuteAsync();

                string fileName = fileInfo.Name;
                string filePath = localFilePath;

                // Перевірка на спеціальні формати Google
                if (fileInfo.MimeType == "application/vnd.google-apps.document")
                {
                    filePath = Path.Combine(directoryPath, fileName + ".docx"); // Експорт как DOCX
                    var exportRequest = _driveService.Files.Export(googleDriveFileId, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await exportRequest.DownloadAsync(stream);
                    }
                }
                else if (fileInfo.MimeType == "application/vnd.google-apps.spreadsheet")
                {
                    filePath = Path.Combine(directoryPath, fileName + ".xlsx"); // Експорт как XLSX
                    var exportRequest = _driveService.Files.Export(googleDriveFileId, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await exportRequest.DownloadAsync(stream);
                    }
                }
                else if (fileInfo.MimeType == "application/vnd.google-apps.presentation")
                {
                    filePath = Path.Combine(directoryPath, fileName + ".pptx"); // Експорт как PPTX
                    var exportRequest = _driveService.Files.Export(googleDriveFileId, "application/vnd.openxmlformats-officedocument.presentationml.presentation");
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await exportRequest.DownloadAsync(stream);
                    }
                }
                else
                {
                    // Звичайні файли, які потребують експорту
                    filePath = Path.Combine(directoryPath, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.DownloadAsync(stream);
                    }
                }

               // MessageBox.Show($"Файл '{fileName}' успішно скачаний в: {filePath}", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при завантаженні файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task DownloadFolderFromGoogleDrive(string folderId, string localFolderPath)
        {
            try
            {
                // Переконаємося, що локальна папка існує
                if (!Directory.Exists(localFolderPath))
                {
                    Directory.CreateDirectory(localFolderPath);
                }

                // Запит вмісту папки на Google Drive
                var request = _driveService.Files.List();
                // Знайти всі файли та папки всередині поточної папки
                request.Q = $"'{folderId}' in parents and trashed = false";
                request.Fields = "files(id, name, mimeType)";
                var response = await request.ExecuteAsync();

                foreach (var item in response.Files)
                {
                    string localItemPath = Path.Combine(localFolderPath, item.Name);

                    if (item.MimeType == "application/vnd.google-apps.folder")
                    {

                        // Рекурсивно завантажуємо вкладену папку
                        await DownloadFolderFromGoogleDrive(item.Id, localItemPath);
                    }
                    else
                    {
                        // Завантажуємо файл
                        await DownloadFileFromGoogleDrive(item.Id, localItemPath);
                    }
                }
            }

            catch (Exception ex)
           {
                MessageBox.Show($"Помилка при завантаженні папки: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }


}
