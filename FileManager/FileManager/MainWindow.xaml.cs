using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Drawing;
using System.Windows.Shell;

namespace FileManager
{
    
    public partial class MainWindow : Window
    {
        private Stack<string> backStack = new Stack<string>();
        private Stack<string> forwardStack = new Stack<string>();
        private string currentFolderPath;
        public int previousFolderPathNumber;
        private string clipboardPath; // Путь к файлу или папке для копирования/вырезания
        private bool isCutOperation; // Определяет, это "вырезать" или "копировать"
        private MenuFunctions _menuFunctions;
        public MainWindow()
        {
            InitializeComponent();
            ShowDrives();
            _menuFunctions = new MenuFunctions();
        }
        private void ShowDrives()
        {
            // Получаем логические диски
            string[] drives = Directory.GetLogicalDrives();

            // Добавляем их в TreeView
            foreach (string drive in drives)
            {
                TreeViewItem driveItem = new TreeViewItem
                {
                    Header = drive, // Отображаемое имя
                    Tag = drive     // Путь к диску
                };

                // Добавляем "заглушку" для возможности раскрытия
                driveItem.Items.Add(null);

                // Подписываемся на событие раскрытия узла
                driveItem.Expanded += FolderExpanded;

                treeView.Items.Add(driveItem);
            }
        }

        private void FolderExpanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)sender;

            // Если элементы уже загружены, пропускаем
            if (item.Items.Count == 1 && item.Items[0] == null)
            {
                item.Items.Clear(); // Убираем "заглушку"

                // Получаем путь
                string path = (string)item.Tag;

                try
                {
                    // Получаем список папок
                    string[] directories = Directory.GetDirectories(path);

                    foreach (string directory in directories)
                    {
                        TreeViewItem subItem = new TreeViewItem
                        {
                            Header = System.IO.Path.GetFileName(directory), // Имя папки
                            Tag = directory                                 // Полный путь
                        };

                        // Добавляем "заглушку" для возможности дальнейшего раскрытия
                        subItem.Items.Add(null);
                        subItem.Expanded += FolderExpanded;

                        item.Items.Add(subItem);
                    }
                }
                catch
                {
                    // Игнорируем ошибки (например, доступ запрещён)
                }
            }
        }
        private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Получаем выбранный узел TreeView
            var selectedNode = treeView.SelectedItem as TreeViewItem;
            if (selectedNode != null)
            {
                // Путь к выбранной папке
                string selectedPath = selectedNode.Tag as string;
                
                if (Directory.Exists(selectedPath))
                {
                    // Сохраняем текущий путь в историю назад                   // Улучшить код убрав копию из treeView и Listview, подготовится к tileview.
                    if (!string.IsNullOrEmpty(currentFolderPath))
                    {
                        backStack.Push(currentFolderPath);
                        makeActiveButton(BackButton);
                    }
                    // Очищаем стек вперёд
                    forwardStack.Clear();

                    // Переходим в новую папку
                    ShowFolderContent(selectedPath);
                }
            }
        }
        
        private void ShowFolderContent(string folderPath)
        {
            fileListView.Items.Clear();

            if (Directory.Exists(folderPath))
            {
                
                    try
                    {
                        // Добавляем папки
                        foreach (var dir in Directory.GetDirectories(folderPath))
                        {
                            fileListView.Items.Add(new FileItem
                            {
                                Name = Path.GetFileName(dir),
                                Type = "Папка",
                                DateModified = Directory.GetLastWriteTime(dir).ToString(),
                                Size = "", // Размер не указывается для папок
                                Icon = "Folder.png"  // Путь к иконке папки
                            });
                        }

                        // Добавляем файлы
                        foreach (var file in Directory.GetFiles(folderPath))
                        {
                            fileListView.Items.Add(new FileItem
                            {
                                Name = Path.GetFileName(file),
                                Type = "Файл",
                                DateModified = File.GetLastWriteTime(file).ToString(),
                                Size = GetReadableFileSize(new FileInfo(file).Length), // Вычисляем размер файла
                                Icon = GetFileIcon(file)// Путь к иконке файла
                            });
                        }
                        currentFolderPath = folderPath;
                        if (forwardStack.Count == 0)
                        {
                            makeInnactiveButton(NextButton);
                        }


                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}");
                    }
            }
        }
        private void OnListViewItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (fileListView.SelectedItem is FileItem selectedItem)
            {
                if (selectedItem.Type == "Папка")
                {
                    // Если это папка, отображаем её содержимое
                    string selectedPath = Path.Combine(currentFolderPath, selectedItem.Name);
                   
                    if (Directory.Exists(selectedPath))
                    {
                       
                        
                            // Сохраняем текущий путь в историю назад
                            if (!string.IsNullOrEmpty(currentFolderPath))
                            {
                                backStack.Push(currentFolderPath);
                                makeActiveButton(BackButton);
                            }

                            // Очищаем стек вперёд
                            forwardStack.Clear();

                            // Переходим в новую папку
                            ShowFolderContent(selectedPath);
                        
                    
                    }
                }
                else if (selectedItem.Type == "Файл")
                {
                    // Если это файл, открываем его информацию (пример: MessageBox)
                    MessageBox.Show($"Открыт файл: {selectedItem.Name}", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
       
        private void OnClickBack(object sender, RoutedEventArgs e)
        {
          

            if (backStack.Count > 0)
            {
                // Перемещаем текущий путь в стек вперёд
                forwardStack.Push(currentFolderPath);

                // Извлекаем предыдущий путь из стека назад
                string previousPath = backStack.Pop();
                if (backStack.Count == 0)
                {
                    makeInnactiveButton(BackButton);
                }
                // Переходим в предыдущую папку
                ShowFolderContent(previousPath);
                if (NextButton.IsEnabled == false)
                {
                    makeActiveButton(NextButton);
                }
            }
            

        }
        private void OnClickForward(object sender, RoutedEventArgs e) {
            if (forwardStack.Count > 0)
            {
                // Перемещаем текущий путь в стек назад
                backStack.Push(currentFolderPath);

                // Извлекаем следующий путь из стека вперёд
                string nextPath = forwardStack.Pop();

                // Переходим в следующую папку
                ShowFolderContent(nextPath);
                if(forwardStack.Count == 0)
                {
                    makeInnactiveButton(NextButton);
                }
                if(BackButton.IsEnabled == false){
                    makeActiveButton(BackButton);
                }
            }
        }

        private void makeInnactiveButton(Button a)
        {
            a.IsEnabled = false;
        }
        private void makeActiveButton(Button a)
        {
            a.IsEnabled = true;
        }
        private string GetReadableFileSize(long sizeInBytes)
        {
            if (sizeInBytes < 1024) return $"{sizeInBytes} Б";
            if (sizeInBytes < 1024 * 1024) return $"{sizeInBytes / 1024.0:F2} КБ";
            if (sizeInBytes < 1024 * 1024 * 1024) return $"{sizeInBytes / (1024.0 * 1024.0):F2} МБ";
            return $"{sizeInBytes / (1024.0 * 1024.0 * 1024.0):F2} ГБ";
        }
      
        // Переключение на вид списка
        private void SwitchToListView(object sender, RoutedEventArgs e)
        {
            fileListView.View = new GridView
            {
                Columns =
            {
            new GridViewColumn { Header = "Ім'я", DisplayMemberBinding = new Binding("Name"), Width = 300 },
            new GridViewColumn { Header = "Тип", DisplayMemberBinding = new Binding("Type"), Width = 100 },
            new GridViewColumn { Header = "Дата зміни", DisplayMemberBinding = new Binding("DateModified"), Width = 150 },
            new GridViewColumn { Header = "Розмір", DisplayMemberBinding = new Binding("Size"), Width = 75 }
            }
            };
            fileListView.ItemContainerStyle = null; // Убираем стиль для TileView
            fileListView.ItemsPanel = new ItemsPanelTemplate
            {
                VisualTree = new FrameworkElementFactory(typeof(StackPanel))
            };
        }

        // Переключение на плиточный вид
        private void SwitchToTileView(object sender, RoutedEventArgs e)
        {
            fileListView.View = null; // Убираем GridView
            fileListView.ItemContainerStyle = (Style)FindResource("TileViewItemStyle");
            fileListView.ItemsPanel = new ItemsPanelTemplate
            {
                VisualTree = new FrameworkElementFactory(typeof(WrapPanel))

            };

        }
        private string GetFileIcon(string filePath)
        {
            // Получаем системную иконку файла
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
            var bitmap = icon.ToBitmap();

            // Конвертируем в путь
            var tempPath = System.IO.Path.GetTempPath() + Guid.NewGuid() + ".png";
            bitmap.Save(tempPath);
            return tempPath;
        }

        private void OnOpenClick(object sender, RoutedEventArgs e)
        {   if(fileListView.View == null) return;
           // MessageBox.Show("Открыть выбранный файл.");
            if (fileListView.SelectedItem is FileItem selectedItem)
            {
                if (selectedItem.Type == "Папка")
                {
                    // Если это папка, отображаем её содержимое
                    string selectedPath = Path.Combine(currentFolderPath, selectedItem.Name);

                    if (Directory.Exists(selectedPath))
                    {


                        // Сохраняем текущий путь в историю назад
                        if (!string.IsNullOrEmpty(currentFolderPath))
                        {
                            backStack.Push(currentFolderPath);
                            makeActiveButton(BackButton);
                        }

                        // Очищаем стек вперёд
                        forwardStack.Clear();

                        // Переходим в новую папку
                        ShowFolderContent(selectedPath);


                    }
                }
                else if (selectedItem.Type == "Файл")
                {
                    // Если это файл, открываем его информацию (пример: MessageBox)
                    MessageBox.Show($"Открыт файл: {selectedItem.Name}", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        private void UpdateFileList(string directoryPath)
        {
            try
            {
                // Очищаем существующие элементы перед обновлением ItemsSource
                fileListView.ItemsSource = null;

                // Получаем список всех файлов и папок в текущей директории
                var directoryInfo = new DirectoryInfo(directoryPath);
                var items = directoryInfo.GetFileSystemInfos();

                // Обновляем источник данных ListView
                fileListView.ItemsSource = items;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении списка: {ex.Message}");
            }
        }
        private void OnCutClick(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedItem is FileItem selectedItem)
            {
                clipboardPath = Path.Combine(currentFolderPath, selectedItem.Name);
                isCutOperation = true;
               // MessageBox.Show($"Элемент \"{selectedItem.Name}\" подготовлен для вырезания.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Выберите файл или папку для вырезания.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedItem is FileItem selectedItem)
            {
                clipboardPath = Path.Combine(currentFolderPath, selectedItem.Name);
                isCutOperation = false;
                //MessageBox.Show($"Элемент \"{selectedItem.Name}\" подготовлен для копирования.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Выберите файл или папку для копирования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(clipboardPath))
            {
                MessageBox.Show("Буфер обмена пуст. Сначала выполните вырезание или копирование.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string destinationPath = Path.Combine(currentFolderPath, Path.GetFileName(clipboardPath));

            try
            {
                _menuFunctions.PasteItem(clipboardPath, destinationPath, isCutOperation);
                clipboardPath = null; // Очищаем буфер после вставки
                ShowFolderContent(currentFolderPath); // Обновляем содержимое
                //MessageBox.Show("Операция выполнена успешно.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при вставке: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedItem is FileItem selectedItem)
            {   
                

                if (selectedItem != null)
                {
                    string selectedPath = Path.Combine(currentFolderPath, selectedItem.Name);
                    //MessageBox.Show("Тут");
                    _menuFunctions.DeleteItem(selectedPath);
                    ShowFolderContent(currentFolderPath);
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите элемент для удаления.");
            }
        }

        private void OnRenameClick(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedItem is FileItem selectedItem)
            {
                // Запрос нового имени через MessageBox
                string newName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Введите новое имя:",
                    "Переименование",
                    selectedItem.Name);

                // Если новое имя введено и не пустое
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    string selectedPath = Path.Combine(currentFolderPath, selectedItem.Name);
                    _menuFunctions.RenameItem(selectedPath, newName);
                    ShowFolderContent(currentFolderPath);
                }
            }
            else
            {
                MessageBox.Show("Выберите файл или папку для переименования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

        }

        private string InputBox(string basicName)
        {
            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Введите новое имя:","Створення файлу",basicName);
            return newName;
        }
       // Добавить возможность вносить имена
        private void OnCreateFolderClick(object sender, RoutedEventArgs e)
        {
            _menuFunctions.CreateItem("Folder", currentFolderPath, InputBox("Нова папка"));
            //UpdateFileList(currentFolderPath);
            ShowFolderContent(currentFolderPath);
        }

        private void OnCreateTxtClick(object sender, RoutedEventArgs e)
        {
            _menuFunctions.CreateItem("Txt", currentFolderPath, InputBox("Новий текстовий документ"));
            //UpdateFileList(currentFolderPath);
            ShowFolderContent(currentFolderPath);
        }


        private void OnCreateWordClick(object sender, RoutedEventArgs e)
        {
            _menuFunctions.CreateItem("Word", currentFolderPath, InputBox("Microsoft Word Document"));
            //UpdateFileList(currentFolderPath);
            ShowFolderContent(currentFolderPath);
        }

        private void OnCreateExcelClick(object sender, RoutedEventArgs e)
        {
            _menuFunctions.CreateItem("Excel", currentFolderPath, InputBox("Microsoft PowerPoint Presentation"));
           // UpdateFileList(currentFolderPath);
            ShowFolderContent(currentFolderPath);
        }

        private void OnCreatePptClick(object sender, RoutedEventArgs e)
        {
            _menuFunctions.CreateItem("Ppt", currentFolderPath, InputBox("Microsoft Excel Worksheet"));
           //UpdateFileList(currentFolderPath);
            ShowFolderContent(currentFolderPath);
        }

    }


}


