using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
namespace FileManager
{
    internal class GoogleDriveMenuFunctions
    {
        private readonly DriveService _driveService;
        private string _cutOrCopyItemId; // ID элемента, который нужно вставить
        private bool _isCutOperation;   // Флаг: true, если операция — вырезание
        public GoogleDriveMenuFunctions(DriveService driveService)
        {
            _driveService = driveService;
        }

        /// <summary>
        /// Создать новый элемент в Google Drive.
        /// </summary>
        public async Task CreateItem(string itemType, string parentId, string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(parentId))
                {
                    throw new ArgumentException("ID родительской папки не может быть пустым.", nameof(parentId));
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Имя элемента не может быть пустым.", nameof(name));
                }
                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = name,
                    Parents = new List<string> { parentId }
                };

                // Определяем MIME-тип в зависимости от типа элемента
                switch (itemType)
                {   
                    //ПРОВЕРКА НА СУЩЕСТВОВАНИЕ ЧТОБЫ писать Нова папка(1)
                    case "Folder":
                        
                        fileMetadata.MimeType = "application/vnd.google-apps.folder";
                        break;
                    case "TextFile": // Добавление поддержки текстового файла
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
                        throw new Exception("Неизвестный тип элемента.");
                }

                // Создаем элемент
                var request = _driveService.Files.Create(fileMetadata);
                request.Fields = "id";
                //Thread.Sleep(1500);
                var file = await request.ExecuteAsync();
                //await Task.Delay(500);
                
                // return file.Id; // Возвращаем ID созданного элемента
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при создании элемента: {ex.Message}");
            }
        }

      
        /// <summary>
        /// Переименовать элемент в Google Drive.
        /// </summary>
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
                throw new Exception($"Ошибка при переименовании элемента: {ex.Message}");
            }
        }

        // <summary>
        /// Копировать элемент (запоминаем его ID).
        /// </summary>
        public void CopyItem(string itemId)
        {
            _cutOrCopyItemId = itemId;
            _isCutOperation = false;
        }

        /// <summary>
        /// Вырезать элемент (запоминаем его ID).
        /// </summary>
        public void CutItem(string itemId)
        {
            _cutOrCopyItemId = itemId;
            _isCutOperation = true;
        }

        /// <summary>
        /// Вставить элемент в указанную папку.
        /// </summary>
        public async Task PasteItem(string destinationFolderId)
        {
            try
            {
                // Получаем информацию о элементе, чтобы определить, является ли он папкой
                var getRequest = _driveService.Files.Get(_cutOrCopyItemId);
                getRequest.Fields = "id, name, mimeType";
                var item = await getRequest.ExecuteAsync();

                bool isFolder = item.MimeType == "application/vnd.google-apps.folder";

                if (_isCutOperation)
                {
                    // Вырезание
                    if (isFolder)
                    {
                        // Вырезать папку
                        await MoveFolder(_cutOrCopyItemId, destinationFolderId);
                    }
                    else
                    {
                        // Вырезать файл
                        await MoveItem(_cutOrCopyItemId, destinationFolderId);
                    }
                }
                else
                {
                    // Копирование
                    if (isFolder)
                    {
                        // Копировать папку
                        await CopyFolder(_cutOrCopyItemId, destinationFolderId);
                    }
                    else
                    {
                        // Копировать файл
                        await CopyItemToFolder(_cutOrCopyItemId, destinationFolderId);
                    }
                }

                // Сбрасываем ID после выполнения операции
                _cutOrCopyItemId = null;
                _isCutOperation = false;

               // MessageBox.Show("Операция успешно завершена!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при вставке элемента: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Ошибка при вставке элемента: {ex.Message}");
            }
        }

        /// <summary>
        /// Переместить элемент в новую папку.
        /// </summary>
        private async Task MoveItem(string itemId, string newParentId)
        {
            try
            {
                // Получаем текущие родительские папки
                //MessageBox.Show($"Получение текущих родителей для itemId={itemId}");
                var getRequest = _driveService.Files.Get(itemId);
                getRequest.Fields = "parents"; // Указываем, что хотим получить список родителей
                var file = await getRequest.ExecuteAsync();

                // Удаляем из текущих родителей и добавляем в новую папку
                //MessageBox.Show($"Перемещение файла: itemId={itemId}, новый родитель={newParentId}");
                var updateRequest = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), itemId);
                updateRequest.AddParents = newParentId;
                updateRequest.RemoveParents = string.Join(",", file.Parents);
                updateRequest.Fields = "id, parents"; // Указываем, что хотим обновить родительские папки

                var result = await updateRequest.ExecuteAsync();

                //MessageBox.Show($"Файл успешно перемещен! Новый родительский ID: {newParentId}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Ошибка при перемещении элемента: {ex.Message}");
            }
        }

        /// <summary>
        /// Копировать элемент в новую папку.
        /// </summary>
        private async Task CopyItemToFolder(string itemId, string parentId, string newFileName = null)
        {
            try
            {    //MessageBox.Show($"Копирование: itemId={itemId}, parentId={parentId}");
                var getRequest = _driveService.Files.Get(itemId);
                getRequest.Fields = "name"; // Запрашиваем только имя файла
                var file = await getRequest.ExecuteAsync();

                // Если новое имя не указано, используем оригинальное имя файла
                string fileName = newFileName ?? file.Name;

                // Настройка метаданных для нового файла
                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = fileName, // Уникальное имя или заданное
                    Parents = new List<string> { parentId }
                };

                // Создание запроса на копирование
                var copyRequest = _driveService.Files.Copy(fileMetadata, itemId);
                copyRequest.Fields = "id, name, parents"; // Поля, которые вы хотите получить в ответе
                copyRequest.SupportsAllDrives = true; // Для работы с Shared Drives
                //MessageBox.Show("Перед выполнением запроса на копирование.");
                // Выполнение запроса
                var result = await copyRequest.ExecuteAsync();

                //MessageBox.Show($"Файл успешно скопирован! Новый ID: {result.Id}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Ошибка при копировании элемента: {ex.Message}");
            }
        }
        private async Task CopyFolder(string folderId, string newParentId, string newFolderName = null)
        {
            try
            {
               // MessageBox.Show($"Копирование папки: folderId={folderId}, newParentId={newParentId}");

                // Получаем метаданные оригинальной папки
                var getRequest = _driveService.Files.Get(folderId);
                getRequest.Fields = "name";
                var folder = await getRequest.ExecuteAsync();

                // Создаем новую папку
                var newFolderMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = newFolderName ?? folder.Name,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = new List<string> { newParentId }
                };
                var createRequest = _driveService.Files.Create(newFolderMetadata);
                createRequest.Fields = "id";
                var newFolder = await createRequest.ExecuteAsync();

                // Получение содержимого папки
                var listRequest = _driveService.Files.List();
                listRequest.Q = $"'{folderId}' in parents";
                listRequest.Fields = "files(id, mimeType)";
                var contents = await listRequest.ExecuteAsync();

                // Рекурсивно копируем содержимое
                foreach (var item in contents.Files)
                {
                    if (item.MimeType == "application/vnd.google-apps.folder")
                    {
                        await CopyFolder(item.Id, newFolder.Id); // Рекурсивно копируем подпапки
                    }
                    else
                    {
                        await CopyItemToFolder(item.Id, newFolder.Id); // Копируем файлы
                    }
                }

               // MessageBox.Show($"Папка {folder.Name} успешно скопирована.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Ошибка при копировании папки: {ex.Message}");
            }
        }
        private async Task MoveFolder(string folderId, string newParentId)
        {
            try
            {
               // MessageBox.Show($"Перемещение папки: folderId={folderId}, newParentId={newParentId}");

                // Перемещаем саму папку
                var getRequest = _driveService.Files.Get(folderId);
                getRequest.Fields = "parents";
                var folder = await getRequest.ExecuteAsync();

                var updateRequest = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), folderId);
                updateRequest.AddParents = newParentId;
                updateRequest.RemoveParents = string.Join(",", folder.Parents);
                updateRequest.Fields = "id, parents";
                await updateRequest.ExecuteAsync();

                // Получение содержимого папки
                var listRequest = _driveService.Files.List();
                listRequest.Q = $"'{folderId}' in parents";
                listRequest.Fields = "files(id, mimeType)";
                var contents = await listRequest.ExecuteAsync();

                // Рекурсивно перемещаем содержимое
                foreach (var item in contents.Files)
                {
                    if (item.MimeType == "application/vnd.google-apps.folder")
                    {
                        await MoveFolder(item.Id, folderId); // Рекурсивно перемещаем подпапки
                    }
                    else
                    {
                        await MoveItem(item.Id, folderId); // Перемещаем файлы
                    }
                }

                //MessageBox.Show($"Папка {folderId} успешно перемещена.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Ошибка при перемещении папки: {ex.Message}");
            }
        }
        public async Task DeleteItem(string itemId)
        {
            try
            {
                // Создание запроса на удаление
                var deleteRequest = _driveService.Files.Delete(itemId);
                await deleteRequest.ExecuteAsync();

               // MessageBox.Show($"Элемент с ID {itemId} успешно удалён.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении элемента: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Ошибка при удалении элемента: {ex.Message}");
            }
        }
        public async Task<Google.Apis.Drive.v3.Data.File> GetItemInfo(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new Exception("ID элемента не может быть пустым.");
            }

            try
            {
                var getRequest = _driveService.Files.Get(itemId);

                // Указываем необходимые поля
                getRequest.Fields = "id, name, mimeType";

                var file = await getRequest.ExecuteAsync();
                return file;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении информации об элементе: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Ошибка при получении информации об элементе: {ex.Message}");
            }
        }
        public async Task DeleteItemWithConfirmation(string itemId)
        {
            try
            {
                // Получение информации о элементе
                var fileInfo = await GetItemInfo(itemId);
                // Подтверждение удаления
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить элемент \"{fileInfo.Name}\"?",
                    "Подтверждение удаления",
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
                MessageBox.Show($"Ошибка при удалении элемента: {ex.Message}");
            }
        }
    }

}
