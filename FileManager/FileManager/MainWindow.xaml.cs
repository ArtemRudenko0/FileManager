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

namespace FileManager
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Stack<string> backStack = new Stack<string>();
        private Stack<string> forwardStack = new Stack<string>();
        private string currentFolderPath;
        public int previousFolderPathNumber;
        public MainWindow()
        {
            InitializeComponent();
            ShowDrives();
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
                        Icon = "folder-icon.png" // Путь к иконке папки
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
                        Icon = "file-icon.png" // Путь к иконке файла
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

    }
}


