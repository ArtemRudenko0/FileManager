using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FileManager
{
    internal class MenuFunctions
    {
        public void CreateItem(string itemType, string currentDirectory, string name)
        {
            try
            {
                string itemName = "Новый элемент";
                string fullPath = "";

                switch (itemType)
                {
                    case "Folder":
                        itemName = name;
                        fullPath = Path.Combine(currentDirectory, itemName);
                        int folderCounter = 1;

                        // Проверяем уникальность имени
                        while (Directory.Exists(fullPath))
                        {
                            itemName = $"Нова папка ({folderCounter++})";
                            fullPath = Path.Combine(currentDirectory, itemName);
                        }

                        Directory.CreateDirectory(fullPath);
                       // MessageBox.Show($"Папка '{itemName}' успешно создана.");
                        break;

                    case "Txt":
                        itemName = name + ".txt";
                        fullPath = Path.Combine(currentDirectory, itemName);
                        int txtCounter = 1;

                        // Проверяем уникальность имени
                        while (File.Exists(fullPath))
                        {
                            itemName = $"Новий текстовий документ ({txtCounter++}).txt";
                            fullPath = Path.Combine(currentDirectory, itemName);
                        }

                        File.Create(fullPath).Close(); // Создаем файл
                       // MessageBox.Show($"Файл '{itemName}' успешно создан.");
                        break;

                    case "Word":
                        itemName = name + ".docx";
                        fullPath = Path.Combine(currentDirectory, itemName);
                        int wordCounter = 1;

                        // Проверяем уникальность имени
                        while (File.Exists(fullPath))
                        {
                            itemName = $"Microsoft Word Document ({wordCounter++}).docx";
                            fullPath = Path.Combine(currentDirectory, itemName);
                        }

                        File.Create(fullPath).Close(); // Создаем файл
                       // MessageBox.Show($"Документ Word '{itemName}' успешно создан.");
                        break;

                    case "Excel":
                        itemName = name + ".xlsx";
                        fullPath = Path.Combine(currentDirectory, itemName);
                        int excelCounter = 1;

                        // Проверяем уникальность имени
                        while (File.Exists(fullPath))
                        {
                            itemName = $"Microsoft Excel Worksheet ({excelCounter++}).xlsx";
                            fullPath = Path.Combine(currentDirectory, itemName);
                        }

                        File.Create(fullPath).Close(); // Создаем файл
                        //MessageBox.Show($"Таблица Excel '{itemName}' успешно создана.");
                        break;

                    case "Ppt":
                        itemName = name + ".pptx";
                        fullPath = Path.Combine(currentDirectory, itemName);
                        int pptCounter = 1;

                        // Проверяем уникальность имени
                        while (File.Exists(fullPath))
                        {
                            itemName = $"Microsoft PowerPoint Presentation ({pptCounter++}).pptx";
                            fullPath = Path.Combine(currentDirectory, itemName);
                        }

                        File.Create(fullPath).Close(); // Создаем файл
                       // MessageBox.Show($"Презентация PowerPoint '{itemName}' успешно создана.");
                        break;

                    default:
                        MessageBox.Show("Неизвестный тип элемента.");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании элемента: {ex.Message}");
            }
        }
        public void DeleteItem(string path)
        {
            try
            {
                // Проверяем, существует ли файл или папка
                if (File.Exists(path))
                {
                    //MessageBox.Show("file");
                    // Удаление файла с перемещением в корзину
                    FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    //MessageBox.Show($"Файл '{Path.GetFileName(path)}' успешно перемещен в корзину.");
                }
                else if (Directory.Exists(path))
                {
                   // MessageBox.Show("directory");
                    // Удаление папки с перемещением в корзину
                    FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    //MessageBox.Show($"Папка '{Path.GetFileName(path)}' успешно перемещена в корзину.");
                }
                else
                {
                    MessageBox.Show("Указанный путь не существует.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}");
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
                    MessageBox.Show("Указанный путь не существует.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при переименовании: {ex.Message}");
            }
        }
        public void CopyDirectory(string sourceDir, string destDir)
        {
            // Создаём целевую папку, если её нет
            Directory.CreateDirectory(destDir);

            // Копируем файлы из исходной папки
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            // Рекурсивно копируем вложенные папки
            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(directory));
                CopyDirectory(directory, destSubDir);
            }
        }

        public void PasteItem(string sourcePath, string destinationPath, bool isCut)
        {
            if (File.Exists(sourcePath)) // Если это файл
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
            else if (Directory.Exists(sourcePath)) // Если это папка
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
                throw new FileNotFoundException("Элемент больше не существует.");
            }
        }
    }
}

