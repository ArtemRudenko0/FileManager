using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FileManager
{
    internal class MenuFunctions
    {
        private readonly Func<string, string> _getFileIcon;

        public MenuFunctions(Func<string, string> getFileIcon)
        {
            _getFileIcon = getFileIcon;
        }

        public string GetIcon(string filePath)
        {
            return _getFileIcon(filePath);
        }
        public void CreateItem(string itemType, string currentDirectory, string name)
        {
            try
            {
                string itemName = "Новий елемент";
                string fullPath = "";

                switch (itemType)
                {
                    case "Folder":
                        itemName = name;
                        fullPath = Path.Combine(currentDirectory, itemName);
                        int folderCounter = 1;

                        // Перевіряємо унікальність імені
                        while (Directory.Exists(fullPath))
                        {
                            itemName = $"Нова папка ({folderCounter++})";
                            fullPath = Path.Combine(currentDirectory, itemName);
                        }

                        Directory.CreateDirectory(fullPath);
                       
                        break;

                    case "Txt":
                        itemName = name + ".txt";
                        fullPath = Path.Combine(currentDirectory, itemName);
                        int txtCounter = 1;

                        // Перевіряємо унікальність імені
                        while (File.Exists(fullPath))
                        {
                            itemName = $"Новий текстовий документ ({txtCounter++}).txt";
                            fullPath = Path.Combine(currentDirectory, itemName);
                        }

                        File.Create(fullPath).Close(); // Створюємо файл
                                                       
                        break;

                    case "Word":
                        itemName = name + ".docx";
                        fullPath = Path.Combine(currentDirectory, itemName);
                        int wordCounter = 1;

                        // Перевіряємо унікальність імені
                        while (File.Exists(fullPath))
                        {
                            itemName = $"Microsoft Word Document ({wordCounter++}).docx";
                            fullPath = Path.Combine(currentDirectory, itemName);
                        }

                        File.Create(fullPath).Close(); // Створюємо файл

                        break;

                    case "Excel":
                        itemName = name + ".xlsx";
                        fullPath = Path.Combine(currentDirectory, itemName);
                        int excelCounter = 1;

                        // Перевіряємо унікальність імені
                        while (File.Exists(fullPath))
                        {
                            itemName = $"Microsoft Excel Worksheet ({excelCounter++}).xlsx";
                            fullPath = Path.Combine(currentDirectory, itemName);
                        }

                        File.Create(fullPath).Close(); // Создаем файл
                       
                        break;

                    case "Ppt":
                        itemName = name + ".pptx";
                        fullPath = Path.Combine(currentDirectory, itemName);
                        int pptCounter = 1;

                        // Перевіряємо унікальність імені
                        while (File.Exists(fullPath))
                        {
                            itemName = $"Microsoft PowerPoint Presentation ({pptCounter++}).pptx";
                            fullPath = Path.Combine(currentDirectory, itemName);
                        }

                        File.Create(fullPath).Close(); // Створюємо файл
                                                       
                        break;

                    default:
                        MessageBox.Show("Невідомий тип елементу.");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при створенні елементу: {ex.Message}");
            }
        }
        public void DeleteItem(string path)
        {
            try
            {

                // Перевіряємо, чи існує файл чи папка
                if (File.Exists(path))
                {
                    //MessageBox.Show("file");
                    // Видалення файлу з переміщенням у кошик
                    FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    
                }
                else if (Directory.Exists(path))
                {
                    // MessageBox.Show("directory");
                    // Видалення папки з переміщенням у кошик
                    FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                   
                }
                else
                {
                    MessageBox.Show("Вказаний шлях не існує.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при видаленні: {ex.Message}");
            }
        }
        public void RenameItem(string currentPath, string newName)
        {
            try
            {
                if (File.Exists(currentPath))
                {
                    string newFilePath = Path.Combine(Path.GetDirectoryName(currentPath), newName);
                    File.Move(currentPath, newFilePath);
                }
                else if (Directory.Exists(currentPath))
                {
                    string newFolderPath = Path.Combine(Path.GetDirectoryName(currentPath), newName);
                    Directory.Move(currentPath, newFolderPath);
                }
                else
                {
                    MessageBox.Show("Вказаний шлях не існує.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при переименовании: {ex.Message}");
            }
        }
        public void CopyDirectory(string sourceDir, string destDir)
        {
            // Створюємо цільову папку, якщо її немає
            Directory.CreateDirectory(destDir);


            // Копіюємо файли з вихідної папки
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            // Рекурсивно копіюємо вкладені папки
            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(directory));
                CopyDirectory(directory, destSubDir);
            }
        }

        public void PasteItem(string sourcePath, string destinationPath, bool isCut)
        {
            if (File.Exists(sourcePath)) // Якщо це файл
            {
                if (isCut)
                {
                    File.Move(sourcePath, destinationPath);
                }
                else
                {
                    File.Copy(sourcePath, destinationPath, overwrite: true);
                }
            }
            else if (Directory.Exists(sourcePath))   // Якщо це папка

            {
                if (isCut)
                {
                    Directory.Move(sourcePath, destinationPath);
                }
                else
                {
                    CopyDirectory(sourcePath, destinationPath);
                }
            }
            else
            {
                throw new FileNotFoundException("Елемент більше не існує.");
            }
        }

         public void SearchLocalFiles(string directoryPath, string searchText, Action<FileItem> onItemFound, CancellationToken cts = default )
          {
              try
              {
                // Пошук папок у поточній директорії
                   
                     var directories = Directory.GetDirectories(directoryPath, $"*{searchText}*");
                  foreach (var dir in directories)
                  {
                    cts.ThrowIfCancellationRequested();
                      var folderItem = new FileItem
                      {
                          Name = Path.GetFileName(dir),
                          Type = "Папка",
                          DateModified = Directory.GetLastWriteTime(dir).ToString(),
                          Size = "",
                          FullPath = dir,
                          Icon = "Folder.png"
                      };
                      onItemFound?.Invoke(folderItem);
                  }

                // Пошук папок у поточній директорії
                var files = Directory.GetFiles(directoryPath, $"*{searchText}*");
                  foreach (var file in files)
                  {
                    cts.ThrowIfCancellationRequested();
                    // Перевірка скасування
                    var fileItem = new FileItem
                      {
                          Name = Path.GetFileName(file),
                          Type = "Файл",
                          DateModified = File.GetLastWriteTime(file).ToString(),
                          Size = MainWindow.GetReadableFileSize(new FileInfo(file).Length),
                          FullPath = file,
                          Icon = GetIcon(file)
                      };
                      onItemFound?.Invoke(fileItem);
                  }

                // Рекурсивний обхід підпапок
                var subDirectories = Directory.GetDirectories(directoryPath);
                  foreach (var subDirectory in subDirectories)
                  {
                    cts.ThrowIfCancellationRequested(); // Перевірка скасування
                    SearchLocalFiles(subDirectory, searchText, onItemFound); // Рекурсивний виклик

                }
            }
            catch (OperationCanceledException)
            {
                
               // MessageBox.Show("Відміна успішка");
            }
            catch (UnauthorizedAccessException)
              {
                // Пропускаємо папки, до яких немає доступу
            }
            catch (Exception ex)
              {
                  MessageBox.Show($"Ошибка поиска: {ex.Message}");
              }
          }
        

    }
}

