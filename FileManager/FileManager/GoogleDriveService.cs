using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace FileManager
{
    internal class GoogleDriveService
    {
        private DriveService _service;
        public DriveService GetDriveService()
        {
            return _service;
        }

        public GoogleDriveService()
        {
            string[] Scopes = { DriveService.Scope.Drive, DriveService.Scope.DriveFile };
            string ApplicationName = "FileManager";

            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore("TokenStore", true)).Result;

                _service = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });
            }
        }


        // Отримання файлів з Google Drive
        public IList<Google.Apis.Drive.v3.Data.File> GetFiles(string folderId = "root")
        {
            var request = _service.Files.List();
            // Фільтр: не видалені файли
            request.Q = $"'{folderId}' in parents and trashed=false";
            
            request.Fields = "files(id, name, mimeType, modifiedTime, size)";
            return request.Execute().Files;

        }
        public void FillGoogleDriveTreeView(TreeView treeView, string folderId = "root")
        {
            try
            {
                GoogleDriveService googleDriveService = new GoogleDriveService();
                var items = googleDriveService.GetFiles(folderId);

                treeView.Items.Clear();

                foreach (var item in items)
                {

                    // Створюємо об'єкт FileItem для зберігання даних про файл/папку
                    var fileItem = new FileItem
                    {
                        Name = item.Name,
                        Type = item.MimeType == "application/vnd.google-apps.folder" ? "Папка" : "Файл",
                        Id = item.Id,
                        DateModified = item.ModifiedTimeDateTimeOffset?.ToString("dd.MM.yyyy HH:mm") ?? "Unknown",
                        Size = item.Size.HasValue ? $"{item.Size.Value / 1024} KB" : "Unknown"
                    };


                    // Створюємо TreeViewItem з прив'язкою до FileItem через Tag
                    TreeViewItem treeItem = new TreeViewItem
                    {
                        Header = fileItem.Name,
                        Tag = fileItem, // Прив'язуємо FileItem
                        IsExpanded = false
                    };

                    // Якщо це папка, додаємо "заглушку" для відображення вкладених елементів
                    if (fileItem.Type == "Папка")
                    {
                        treeItem.Items.Add(null); // Заглушка
                        treeItem.Expanded += (s, e) =>
                        {
                            if (treeItem.Items.Count == 1 && treeItem.Items[0] == null)
                            {
                                treeItem.Items.Clear();
                                FillGoogleDriveTreeViewItem(treeItem, fileItem.Id);
                            }
                        };
                    }

                    treeView.Items.Add(treeItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void FillGoogleDriveTreeViewItem(TreeViewItem parentItem, string folderId)
        {
            try
            {
                GoogleDriveService googleDriveService = new GoogleDriveService();
                var items = googleDriveService.GetFiles(folderId);

                foreach (var item in items)
                {
                    // Створюємо об'єкт FileItem
                    var fileItem = new FileItem
                    {
                        Name = item.Name,
                        Type = item.MimeType == "application/vnd.google-apps.folder" ? "Папка" : "Файл",
                        Id = item.Id,
                        DateModified = item.ModifiedTimeDateTimeOffset?.ToString("dd.MM.yyyy HH:mm") ?? "Unknown",
                        Size = item.Size.HasValue ? $"{item.Size.Value / 1024} KB" : "Unknown"
                    };


                    // Створюємо TreeViewItem із прив'язкою до FileItem
                    TreeViewItem treeItem = new TreeViewItem
                    {
                        Header = fileItem.Name,
                        Tag = fileItem,
                        IsExpanded = false
                    };
                    // Якщо це папка, додаємо "заглушку" для вкладених елементів
                    if (fileItem.Type == "Папка")
                    {
                        treeItem.Items.Add(null);
                        treeItem.Expanded += (s, e) =>
                        {
                            if (treeItem.Items.Count == 1 && treeItem.Items[0] == null)
                            {
                                treeItem.Items.Clear();
                                FillGoogleDriveTreeViewItem(treeItem, fileItem.Id);
                            }
                        };
                    }

                    parentItem.Items.Add(treeItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public async Task FillGoogleDriveListView(ListView listView, string folderId = "root")
        {
            try
            {
                // Отримуємо файли з папки
                var request = _service.Files.List();
                request.Fields = "files(id, name, mimeType, modifiedTime, size,iconLink, thumbnailLink)";
                request.Q = $"'{folderId}' in parents and trashed = false";

                var response = await request.ExecuteAsync();

                // Очищаємо вміст ListView
                listView.Items.Clear();

                // Додаємо файли та папки до ListView
                foreach (var item in response.Files)
                {
                    var fileItem = new FileItem
                    {
                        Name = item.Name,
                        Type = item.MimeType.Contains("folder") ? "Папка" : "Файл",
                        Id = item.Id,
                        DateModified = item.ModifiedTimeDateTimeOffset?.ToString("dd.MM.yyyy HH:mm") ?? "Unknown",
                        //Size = item.Size.HasValue ? $"{item.Size.Value / 1024} KB" : "",
                        Size = item.Size.HasValue? MainWindow.GetReadableFileSize(item.Size.Value) : "",
                        Icon = item.MimeType.Contains("folder") ? "folder.png" : item.ThumbnailLink //Встановлюємо посилання на іконку
                    };
                    //Якщо немає великої іконки ThumbnailLink, то встановлюємо iconLink
                    if (fileItem.Icon == null)
                    {
                        fileItem.Icon = item.IconLink;
                    }
                    listView.Items.Add(fileItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
         public async Task SearchGoogleDriveRecursive(string searchText, string currentFolderId = "root", Action<FileItem> onItemFound = null, CancellationToken cts = default)
         {
            

            try
             {

                // Черга для обходу папок
                Queue<string> foldersToSearch = new Queue<string>();
                 foldersToSearch.Enqueue(currentFolderId);

                 while (foldersToSearch.Count > 0)
                 {
                    cts.ThrowIfCancellationRequested();
                    string folderId = foldersToSearch.Dequeue();

                     var request = _service.Files.List();
                     request.Fields = "files(id, name, mimeType, modifiedTime, size,iconLink, thumbnailLink, parents)";
                     request.Q = $"trashed = false and '{folderId}' in parents";

                     var response = await request.ExecuteAsync(cts);

                    foreach (var file in response.Files)
                    {
                        cts.ThrowIfCancellationRequested();

                        // Перевіряємо, чи відповідає ім'я умовам пошуку
                        if (file.Name.Contains(searchText))
                        {
                            var fileItem = new FileItem
                            {
                                Name = file.Name,
                                Type = file.MimeType.Contains("folder") ? "Папка" : "Файл",
                                DateModified = file.ModifiedTimeDateTimeOffset?.ToString("g") ?? "-",
                                Id = file.Id,
                                Size = file.Size.HasValue ? MainWindow.GetReadableFileSize(file.Size.Value) : "",
                                Icon = file.MimeType.Contains("folder") ? "folder.png" : file.ThumbnailLink
                            };
                            if (fileItem.Icon == null)
                            {
                                fileItem.Icon = file.IconLink;
                            }
                            onItemFound?.Invoke(fileItem);
                         }
                        // Якщо поточний файл - папка, додаємо її до черги
                        if (file.MimeType.Contains("folder"))
                         {
                             foldersToSearch.Enqueue(file.Id);
                         }
                     }
                 }
             }
            catch (OperationCanceledException)
            {
             
            }
            catch (Exception ex)
             {
                 MessageBox.Show($"Помилка рекурсивного пошуку на Google Диску: {ex.Message}");
             }
         }
        
        public async Task<List<BreadcrumbItem>> GetGoogleDriveBreadcrumbs(string currentFolderId)
        {
            List<BreadcrumbItem> breadcrumbs = new List<BreadcrumbItem>();

            while (!string.IsNullOrEmpty(currentFolderId))
            {
                // Отримуємо інформацію про поточну папку
                var folderInfo = await GetFolderInfo(currentFolderId);
                if (folderInfo == null) break;

                // Додаємо на початок списку
                breadcrumbs.Insert(0, new BreadcrumbItem
                {
                    Name = folderInfo.Name,
                    FullPath = folderInfo.Id,
                    IsGoogleDrive = true
                });

                // Переходимо до батьківської папки
                currentFolderId = folderInfo.FullPath;
            }

            return breadcrumbs;
        }
        public async Task<FileItem> GetFolderInfo(string folderId)
        {
            try
            {
                var request = _service.Files.Get(folderId);
                request.Fields = "id, name, parents";

                var file = await request.ExecuteAsync();
                return new FileItem
                {
                    Id = file.Id,
                    Name = file.Name,
                    FullPath = file.Parents?.FirstOrDefault()  // Google Drive може мати кілька батьків

                };
            }
            catch (Exception ex)
            {

                // Обробка помилок
                MessageBox.Show($"Помилка отримання інформації про папку: {ex.Message}");
                return null;
            }
        }
    }
}
    
