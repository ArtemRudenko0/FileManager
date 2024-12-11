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
            string[] Scopes = { DriveService.Scope.Drive };
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

        // Получение файлов из Google Drive
        public IList<Google.Apis.Drive.v3.Data.File> GetFiles(string folderId = "root")
        {
            var request = _service.Files.List();
            request.Q = $"'{folderId}' in parents and trashed=false"; // Фильтр: не удаленные файлы
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
                    // Создаём объект FileItem для хранения данных о файле/папке
                    var fileItem = new FileItem
                    {
                        Name = item.Name,
                        Type = item.MimeType == "application/vnd.google-apps.folder" ? "Папка" : "Файл",
                        Id = item.Id,
                        DateModified = item.ModifiedTimeDateTimeOffset?.ToString("dd.MM.yyyy HH:mm") ?? "Unknown",
                        Size = item.Size.HasValue ? $"{item.Size.Value / 1024} KB" : "Unknown"
                    };

                    // Создаём TreeViewItem с привязкой к FileItem через Tag
                    TreeViewItem treeItem = new TreeViewItem
                    {
                        Header = fileItem.Name,
                        Tag = fileItem, // Привязываем FileItem
                        IsExpanded = false
                    };

                    // Если это папка, добавляем "заглушку" для отображения вложенных элементов
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
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    // Создаём объект FileItem
                    var fileItem = new FileItem
                    {
                        Name = item.Name,
                        Type = item.MimeType == "application/vnd.google-apps.folder" ? "Папка" : "Файл",
                        Id = item.Id,
                        DateModified = item.ModifiedTimeDateTimeOffset?.ToString("dd.MM.yyyy HH:mm") ?? "Unknown",
                        Size = item.Size.HasValue ? $"{item.Size.Value / 1024} KB" : "Unknown"
                    };

                    // Создаём TreeViewItem с привязкой к FileItem
                    TreeViewItem treeItem = new TreeViewItem
                    {
                        Header = fileItem.Name,
                        Tag = fileItem,
                        IsExpanded = false
                    };

                    // Если это папка, добавляем "заглушку" для вложенных элементов
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
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void FillGoogleDriveListView(ListView listView, string folderId = "root")
        {
            try
            {
                // Получаем файлы из папки
                var items = GetFiles(folderId);

                // Очищаем содержимое ListView
                listView.Items.Clear();

                // Добавляем файлы и папки в ListView
                foreach (var item in items)
                {
                    var fileItem = new FileItem
                    {
                        Name = item.Name,
                        Type = item.MimeType == "application/vnd.google-apps.folder" ? "Папка" : "Файл",
                        Id = item.Id,
                        DateModified = item.ModifiedTimeDateTimeOffset?.ToString("dd.MM.yyyy HH:mm") ?? "Unknown",
                        Size = item.Size.HasValue ? $"{item.Size.Value / 1024} KB" : ""
                    };

                    listView.Items.Add(fileItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
