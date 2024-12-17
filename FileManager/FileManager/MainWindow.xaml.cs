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
using Brushes = System.Windows.Media.Brushes;
using System.Threading;
using Google.Apis.Drive.v3;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Diagnostics;

namespace FileManager
{
    
    public partial class MainWindow : Window
    {   
        //Стеки для переходів між сторінками в локальних папках
        private Stack<string> backStack = new Stack<string>();
        private Stack<string> forwardStack = new Stack<string>();
        //Стеки для переходів між сторінками на гугл диску
        private Stack<string> googleDriveBackStack = new Stack<string>();
        private Stack<string> googleDriveForwardStack = new Stack<string>();
        //Початкові значеня сторінок для гугл диску та локальних файлів
        private string currentGoogleFolderId = "root";      
        private string currentFolderPath = "Диски";

        public int previousFolderPathNumber; // Номер попередньої сторінки в стеці
        //Шляхи до файлу або папки для копіювання/вирізання в локальних файлах та гугл диску
        private string clipboardPath;
        private string clipboardGoogleDriveItemId;
        private bool isCutOperation; // Визначення операції копіювання чи вирізання
        private MenuFunctions _menuFunctions;   // Клас з функціями для меню, яке викликається правою кнопкою миші
        private GoogleDriveService _googleDriveService; //Клас з функціями для роботи з гугл диском
        private GoogleDriveMenuFunctions _googleDriveMenuFunctions; //Клас з функціями для меню на гугл диску
        private CancellationTokenSource _debounceTokenSource;   //Токен відміни для затримки зчитування символів з пошукової строки
        private CancellationTokenSource _cancelSearchTokenSource;   //Токен відміни пошуку при будь-якій іншій взаємодії з програмою
         //Початкові значення для кнопки "Назад" та "Вперед"                                      
        private bool backButtonState = false;   
        private bool forwardButtonState = false;
        private bool isDiskStyle; // Перевірка чи встановлено зараз стиль відображення ListView для дисків 
        private string currentStyle = "ListView";   //Початковий стиль для відображення списку папок

        //Змінна для вибору функцій: Якщо значення false - то виконується блок команд, призначених для локальних файлів. True - команди для Google Drive
        bool IsGoogleDrive = false; 
           
        //Ініціація головного вікна зі створенням класів
        public MainWindow()
        {
            InitializeComponent();
            ShowDrives();
            ShowLocalDrives();
            _menuFunctions = new MenuFunctions(GetCachedIcon);
            _googleDriveService = new GoogleDriveService(); // Инициализация Google Drive API
            _googleDriveMenuFunctions = new GoogleDriveMenuFunctions(_googleDriveService.GetDriveService());
            _cancelSearchTokenSource = new CancellationTokenSource();
        }
        //Заповнення TreeView наявними дисками
        private void ShowDrives()
        {
            treeView.Items.Clear();
            // Отримуємо логічні диски
            string[] drives = Directory.GetLogicalDrives();

            //Додаємо їх у TreeView
            foreach (string drive in drives)
            {
                TreeViewItem driveItem = new TreeViewItem
                {
                    Header = drive, // Ім'я
                    Tag = drive     // Шлях до диску
                };

                // Додаємо "заглушку" для можливості розкриття
                driveItem.Items.Add(null);

                //Підписуємося на подію розкриття вузла
                driveItem.Expanded += FolderExpanded;

                treeView.Items.Add(driveItem);
            }
            
        }
        //Заповнення ListView наявними дисками
        private void ShowLocalDrives()
        {

            var allDrives = DriveInfo.GetDrives();
            currentFolderPath = "Диски"; // Встановлюємо поточний шлях як "Диски"

            // Додаємо "Диски" у стек назад, якщо він не дублюється
            if (backStack.Count == 0 || backStack.Peek() != "Диски")
            {
                backStack.Push(currentFolderPath);
            }
            SwitchToDiskView();
            
            fileListView.ItemContainerStyle = (Style)FindResource("TileViewDiskStyle");
            foreach (var drive in allDrives)
            {
                if (drive.IsReady) // Перевіряємо, чи диск готовий
                {
                    var fileItem = new FileItem
                    {
                        Name = drive.Name, // Ім'я диску
                        Type = "Диск",
                        Size = $"{GetReadableFileSize(drive.TotalFreeSpace)} свободно из {GetReadableFileSize(drive.TotalSize)}",
                        DateModified = "-",
                        FullPath = drive.Name,
                        Icon = "drive.png", 
                        TotalSize = drive.TotalSize,
                        FreeSpace = drive.TotalFreeSpace
                    };

                    fileListView.Items.Add(fileItem);
                }
            }
            makeInnactiveButton(ListButton);
            makeInnactiveButton(TileButton);
            SearchTextBox.IsEnabled = false;
            UpdateBreadcrumbs("Диски", false);
         
            UpdateNavigationButtons();
            makeInnactiveButton(HomeButton);

        }
        private void FolderExpanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)sender;


            // Якщо елементи вже завантажені, пропускаємо
            if (item.Items.Count == 1 && item.Items[0] == null)
            {
                item.Items.Clear(); // Забираємо "заглушку"

                //Отримуємо шлях
                string path = (string)item.Tag;

                try
                {
                    //Отримуємо список папок
                    string[] directories = Directory.GetDirectories(path);

                    foreach (string directory in directories)
                    {
                        TreeViewItem subItem = new TreeViewItem
                        {
                            Header = System.IO.Path.GetFileName(directory), // Ім'я папки
                            Tag = directory                                 // Повний шлях
                        };

                        // Додаємо "заглушку" для можливості подальшого розкриття
                        subItem.Items.Add(null);
                        subItem.Expanded += FolderExpanded;

                        item.Items.Add(subItem);
                    }
                }
                catch
                {
                    // Ігноруємо помилки (наприклад, доступ заборонено)
                }
            }
        }
        private async void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Отримуємо обраний вузол TreeView
            var selectedNode = treeView.SelectedItem as TreeViewItem;
            if (selectedNode != null)
            {
                await CancelTask(_cancelSearchTokenSource);
                // Шлях до обраної папки
                string selectedPath = selectedNode.Tag as string;
                if (selectedNode?.Tag is FileItem selectedItem)
                {
                    if (IsGoogleDrive) // Перевірка активності вкладки
                    {
                        if (selectedItem.Type == "Папка" || selectedItem.Type == "Диск")
                        {
                            // Якщо це папка в Google Drive, завантажуємо її вміст
                            string folderId = selectedItem.Id; 
                            googleDriveBackStack.Push(currentGoogleFolderId);
                            makeActiveButton(BackButton);
                            googleDriveForwardStack.Clear();
                            ShowGoogleDriveFolderContent(folderId);
                            
                        }
                        else if (selectedItem.Type == "Файл")
                        {
                             string fileUrl = $"https://drive.google.com/file/d/{selectedItem.Id}/edit";

                        try
                        {
                            // Відкриваємо посилання у браузері за замовчуванням
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = fileUrl,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Не вдалося відкрити файл: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                            
                        }
                    }
                }
                else
                {
                    if (Directory.Exists(selectedPath))
                    {

                        // Зберігаємо поточний шлях до історії тому              
                        if (!string.IsNullOrEmpty(currentFolderPath))
                        {
                            backStack.Push(currentFolderPath);
                            makeActiveButton(BackButton);
                        }
                        // Очищаємо стек вперед
                        forwardStack.Clear();
                        makeActiveButton(HomeButton);

                        // Переходимо до нової папки
                        await ShowFolderContent(selectedPath);
                    }
                }
            }
        }
       
        private async Task ShowFolderContent(string folderPath)
        {
           
            fileListView.ItemsSource = null;
            fileListView.Items.Clear();
            if (folderPath == "Диски")
            {
                ShowLocalDrives();
                
            }
            else
            {
                isDiskStyle = false;
                if (Directory.Exists(folderPath))
                {
                    fileListView.ItemContainerStyle = (Style)FindResource("TileViewDiskStyle");

                    try
                    {

                        // Додаємо папки
                        foreach (var dir in Directory.GetDirectories(folderPath))
                        {
                            fileListView.Items.Add(new FileItem
                            {
                                Name = Path.GetFileName(dir),
                                Type = "Папка",
                                FullPath = dir,
                                DateModified = Directory.GetLastWriteTime(dir).ToString(),
                                Size = "", // Розмір не вказується для папок
                                Icon = "Folder.png"  // Шлях до іконки папки
                            });
                        }

                        // Додаємо файли
                        foreach (var file in Directory.GetFiles(folderPath))
                        {
                            fileListView.Items.Add(new FileItem
                            {
                                Name = Path.GetFileName(file),
                                Type = "Файл",
                                FullPath = file,
                                DateModified = File.GetLastWriteTime(file).ToString(),
                                // Обчислюємо розмір файлу
                                Size = GetReadableFileSize(new FileInfo(file).Length),
                                // Шлях до іконки файлу
                                Icon = GetCachedIcon(file)

                            });
                        }
                        // Додаємо шлях у стек, якщо він не дублюється
                        if (backStack.Count == 0 || backStack.Peek() != folderPath)
                        {
                            backStack.Push(folderPath);
                        }
                        if (isDiskStyle == false)
                        {
                            ChooseStyle(currentStyle);
                        }
                        currentFolderPath = folderPath;
                        UpdateNavigationButtons();
                        UpdateBreadcrumbs(folderPath, false);
                        makeActiveButton(HomeButton);
                        makeActiveButton(ListButton);
                        makeActiveButton(TileButton);
                        SearchTextBox.IsEnabled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}");
                    }

                }
            }
        }
        private async void OnListViewItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            
            if (fileListView.SelectedItem is FileItem selectedItem)
            {
                await CancelTask(_cancelSearchTokenSource);
                if (IsGoogleDrive) 
                {
                    if (selectedItem.Type == "Папка")
                    {
                        // Якщо це папка в Google Drive, завантажуємо її вміст
                        string folderId = selectedItem.Id; // Передбачається, що FileItem містить поле Id для Google Drive
                        googleDriveBackStack.Push(currentGoogleFolderId);
                        makeActiveButton(BackButton);
                        googleDriveForwardStack.Clear();
                        ShowGoogleDriveFolderContent(folderId);
                       
                    }
                    else if (selectedItem.Type == "Файл")
                    {
                        string fileUrl = $"https://drive.google.com/file/d/{selectedItem.Id}/edit";

                        try
                        {

                            // Відкриваємо посилання у браузері за замовчуванням
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = fileUrl,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Не вдалося відкрити файл: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        
                        
                    }
                }
                else
                {
                    string selectedPath;

                    
                    selectedPath = selectedItem.FullPath;
                
                    if (selectedItem.Type == "Папка" || selectedItem.Type == "Диск")
                    {

                        if (Directory.Exists(selectedPath))
                        {

                            // Зберігаємо поточний шлях до історії тому
                            if (!string.IsNullOrEmpty(currentFolderPath))
                            {

                                makeActiveButton(BackButton);
                                
                            }
                            else if (selectedItem.Type == "Диск")
                            {
                                
                                makeActiveButton(BackButton);
                                
                            }
                            // Очищаємо стек вперед
                            forwardStack.Clear();
                            // Переходимо до нової папки
                            makeActiveButton(HomeButton);
                            
                            await ShowFolderContent(selectedPath);


                        }
                    }
                    else if (selectedItem.Type == "Файл")
                    {
                        try
                        {

                            // Запускаємо файл за допомогою асоційованої програми
                            Process.Start(new ProcessStartInfo
                            {
                                // Повний шлях до файлу
                                FileName = selectedItem.FullPath,
                                // Використовуємо оболонку Windows для відкриття
                                UseShellExecute = true, 
                                WorkingDirectory = Path.GetDirectoryName(selectedItem.FullPath),
                                Arguments = "-fullscreen -high", 
                            });
                        }
                        catch (Exception ex)
                        {
                            // Обрабатываем ошибки, например, если файл не поддерживается
                            MessageBox.Show($"Не вдалося відкрити файл: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        
                    }
                }
            }

        }
        
        private async void OnHomeClick(object sender, RoutedEventArgs e)
        {
            await CancelTask(_cancelSearchTokenSource);// Скасуємо поточні операції


            // Очищаємо стеки навігації
            forwardStack.Clear();


            // Завантажуємо список дисків
            fileListView.ItemsSource = null;
            fileListView.Items.Clear();
            ShowLocalDrives();
            // Оновлюємо шлях та хлібні крихти
            UpdateBreadcrumbs("Диски", false);
        }
         private async void OnClickBack(object sender, RoutedEventArgs e)
         {
             await CancelTask(_cancelSearchTokenSource);
             if (IsGoogleDrive)// Перевірка поточної вкладки
            {
                 if (googleDriveBackStack.Count > 0)
                 {

                    // Зберігаємо потокову папку для переходу вперед
                    googleDriveForwardStack.Push(currentGoogleFolderId);

                    // Переходимо до попередньої папки
                    string previousFolderId = googleDriveBackStack.Pop();
                     if (googleDriveBackStack.Count == 0)
                     {
                         makeInnactiveButton(BackButton);
                     }
                    // Переходимо до попередньої папки

                    if (NextButton.IsEnabled == false)
                     {
                         makeActiveButton(NextButton);
                     }
                     ShowGoogleDriveFolderContent(previousFolderId);
                 }
             }
             else
             {
                
               
                if (backStack.Count > 1) // Якщо в стеку більше одного елемента
                {
                    // Додаємо поточний шлях у стек "Вперед", якщо він не дублюється
                    if (!string.IsNullOrEmpty(currentFolderPath) &&
                        (forwardStack.Count == 0 || forwardStack.Peek() != currentFolderPath))
                    {
                        forwardStack.Push(currentFolderPath);
                        makeActiveButton(NextButton);
                    }

                    // Витягуємо попередній шлях зі стека
                    backStack.Pop(); // Видаляємо поточний шлях
                    string previousPath = backStack.Peek(); // Беремо попередній шлях

                    // Переходимо до попередньої папки
                    await ShowFolderContent(previousPath);

                    // Відключаємо кнопку "Назад", якщо стек порожній
                    if (backStack.Count <= 1)
                    {
                        makeInnactiveButton(BackButton);
                    }
                }
            }

         }

        private async void OnClickForward(object sender, RoutedEventArgs e) {
            await CancelTask(_cancelSearchTokenSource);
            if (IsGoogleDrive) // Перевірка поточної вкладки
            {
                if (googleDriveForwardStack.Count > 0)
                {
                    // Зберігаємо поточну папку в історію тому
                    googleDriveBackStack.Push(currentGoogleFolderId);

                    // Переходимо до наступної папки
                    string nextFolderId = googleDriveForwardStack.Pop();
                    ShowGoogleDriveFolderContent(nextFolderId);
                    if (googleDriveForwardStack.Count == 0)
                    {
                        makeInnactiveButton(NextButton);
                    }
                    if (BackButton.IsEnabled == false)
                    {
                        makeActiveButton(BackButton);
                    }
                }
            }
            else
            {
                
                if (forwardStack.Count > 0)
                {
                   
                    makeActiveButton(BackButton);

                    // Витягуємо наступний шлях зі стека
                    string nextPath = forwardStack.Pop();

                    // Переходимо до наступної папки
                    await ShowFolderContent(nextPath);

                    // Відключаємо кнопку "Вперед", якщо стек порожній
                    if (forwardStack.Count == 0)
                    {
                        makeInnactiveButton(NextButton);
                    }
                    makeActiveButton(HomeButton);
                }
            }
        }
        
        private void UpdateNavigationButtons()
        {
            if (backStack.Count > 1 )
                makeActiveButton(BackButton);
            else
                makeInnactiveButton(BackButton);

            if (forwardStack.Count > 0)
                makeActiveButton(NextButton);
            else
                makeInnactiveButton(NextButton);
        }

       // Функції активації дезактивації кнопок
        private void makeInnactiveButton(Button a)
        {
            a.IsEnabled = false;
        }
        private void makeActiveButton(Button a)
        {
            a.IsEnabled = true;
        }
        public static string GetReadableFileSize(long sizeInBytes)
        {
            if (sizeInBytes < 1024) return $"{sizeInBytes} Б";
            if (sizeInBytes < 1024 * 1024) return $"{sizeInBytes / 1024.0:F2} КБ";
            if (sizeInBytes < 1024 * 1024 * 1024) return $"{sizeInBytes / (1024.0 * 1024.0):F2} МБ";
            return $"{sizeInBytes / (1024.0 * 1024.0 * 1024.0):F2} ГБ";
        }
      
        // Переключення на вид списку
        private void SwitchToListView(object sender, RoutedEventArgs e)
        {
            makeSwitchToListView();
        }
        private void makeSwitchToListView()
        {
           
            currentStyle = "ListView";
            isDiskStyle = false;

            // Створюємо колонку для імені з іконкою та текстом
            var nameColumn = new GridViewColumn
            {
                Header = "Ім'я",
                Width = 300,
                CellTemplate = new DataTemplate()
            };

            // Створюємо шаблон для колонки
            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            // Додаємо іконку
            var imageFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
            imageFactory.SetValue(System.Windows.Controls.Image.WidthProperty, 16.0);
            imageFactory.SetValue(System.Windows.Controls.Image.HeightProperty, 16.0);
            imageFactory.SetValue(System.Windows.Controls.Image.MarginProperty, new Thickness(0, 0, 5, 0));
            imageFactory.SetBinding(System.Windows.Controls.Image.SourceProperty, new Binding("Icon"));
            stackPanelFactory.AppendChild(imageFactory);

            // Додаємо текст
            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            stackPanelFactory.AppendChild(textBlockFactory);

            // Встановлюємо візуальне дерево для комірки
            nameColumn.CellTemplate.VisualTree = stackPanelFactory;


            // Створюємо GridView і додаємо інші колонки
            var gridView = new GridView();
            gridView.Columns.Add(nameColumn);
            gridView.Columns.Add(new GridViewColumn
            {
                Header = "Тип",
                DisplayMemberBinding = new Binding("Type"),
                Width = 100
            });
            gridView.Columns.Add(new GridViewColumn
            {
                Header = "Дата зміни",
                DisplayMemberBinding = new Binding("DateModified"),
                Width = 150
            });
            gridView.Columns.Add(new GridViewColumn
            {
                Header = "Розмір",
                DisplayMemberBinding = new Binding("Size"),
                Width = 75
            });

            // Застосовуємо GridView до ListView
            fileListView.View = gridView;

            // Забираємо стилі для інших режимів
            fileListView.ItemContainerStyle = null;
            fileListView.ItemsPanel = new ItemsPanelTemplate
            {
                VisualTree = new FrameworkElementFactory(typeof(StackPanel))
            };
        }
        private void SwitchToDiskView()
        {
            isDiskStyle = true;
            fileListView.View = null; // Прибираємо GridView
            fileListView.ItemContainerStyle = (Style)FindResource("TileViewDiskStyle");
            fileListView.ItemsPanel = new ItemsPanelTemplate
            {
                VisualTree = new FrameworkElementFactory(typeof(WrapPanel))

            };
        }
        // Перемикання на плитковий вигляд
        private void SwitchToTileView(object sender, RoutedEventArgs e)
        {
            makeSwitchToTileView();

        }
        private void makeSwitchToTileView()
        {
            currentStyle = "TileView";
            isDiskStyle = false;
            fileListView.View = null; // Прибираємо GridView
            fileListView.ItemContainerStyle = (Style)FindResource("TileViewItemStyle");
            fileListView.ItemsPanel = new ItemsPanelTemplate
            {
                VisualTree = new FrameworkElementFactory(typeof(WrapPanel))

            };
        }
        private void ChooseStyle(string style)
        {
            if (style == "ListView")
            {
                makeSwitchToListView();
            }
            else if (style == "TileView")
            {
                makeSwitchToTileView();
            }
        }

        //Словник для кешованих іконок
        private static Dictionary<string, string> IconCache = new Dictionary<string, string>();

        private static string GetCachedIcon(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!IconCache.ContainsKey(extension))
            {
                IconCache[extension] = GetFileIcon(filePath); // Отримуємо іконку лише один раз
            }
            return IconCache[extension];
        }
        public static string GetFileIcon(string filePath)
        {
            //Отримуємо системну іконку файлу
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
            if (icon == null) return null;

            // Конвертуємо Icon у Bitmap з прозорістю
            using (Bitmap originalBitmap = icon.ToBitmap())
            {
                // Збільшуємо роздільну здатність: 128x128 або 256x256
                int targetWidth = 256;
                int targetHeight = 256;

                Bitmap resizedBitmap = new Bitmap(targetWidth, targetHeight);

                using (Graphics g = Graphics.FromImage(resizedBitmap))
                {
                    // Поліпшення якості рендерингу
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    // Малюємо зображення з масштабуванням
                    g.DrawImage(originalBitmap, new System.Drawing.Rectangle(0, 0, targetWidth, targetHeight));
                }

                // Зберігаємо зображення як PNG
                string tempPath = System.IO.Path.GetTempPath() + Guid.NewGuid() + ".png";
                resizedBitmap.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);

                return tempPath;
            }

        }

        private async void OnOpenClick(object sender, RoutedEventArgs e)
        {
            
            if (fileListView.View == null) return;
           
            if (fileListView.SelectedItem is FileItem selectedItem)
            {
                await CancelTask(_cancelSearchTokenSource);
                if (IsGoogleDrive)
                
                {
                    if (selectedItem.Type == "Папка")
                    {

                        // Якщо це папка в Google Drive, завантажуємо її вміст
                        string folderId = selectedItem.Id; 
                        googleDriveBackStack.Push(currentGoogleFolderId);
                        makeActiveButton(BackButton);
                        googleDriveForwardStack.Clear();
                        ShowGoogleDriveFolderContent(folderId);

                    }
                    else if (selectedItem.Type == "Файл")
                    {
                        string fileUrl = $"https://drive.google.com/file/d/{selectedItem.Id}/edit";

                        try
                        {
                            // Відкриваємо посилання у браузері за замовчуванням
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = fileUrl,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Не вдалося відкрити файл: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    if (selectedItem.Type == "Папка" || selectedItem.Type == "Диск")
                    {
                        // Якщо це папка, відображаємо її вміст
                        string selectedPath;
                        selectedPath = selectedItem.FullPath;
                        if (Directory.Exists(selectedPath))
                        {

                            
                         
                            if (!string.IsNullOrEmpty(currentFolderPath))
                            {
                                
                                makeActiveButton(BackButton);
                            }
                            if (selectedItem.Type == "Диск")
                            {
                                
                                makeActiveButton(BackButton);
                            }
                            // Очищаємо стек вперед
                            forwardStack.Clear();
                            makeActiveButton(HomeButton);
                            // Переходимо до нової папки
                            await ShowFolderContent(selectedPath);


                        }
                    }
                    else if (selectedItem.Type == "Файл")
                    {
                        try
                        {
                            // Запускаємо файл за допомогою асоційованої програми
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = selectedItem.FullPath, // Повний шлях до файлу
                                UseShellExecute = true, // Використовуємо оболонку Windows для відкриття
                                WorkingDirectory = Path.GetDirectoryName(selectedItem.FullPath),
                                Arguments = "-fullscreen -high",
                          
                            });
                        }
                        catch (Exception ex)
                        {

                            // Обробляємо помилки, наприклад, якщо файл не підтримується
                            MessageBox.Show($"Не вдалося відкрити файл: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }
       
        private void OnCutClick(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedItem is FileItem selectedItem)
            {
                if (IsGoogleDrive)  // Перевірка активної вкладки
                {
                    // Використовуємо Google Drive для вирізування
                    clipboardGoogleDriveItemId = selectedItem.Id; // Зберігаємо ID елемента
                    _googleDriveMenuFunctions.CutItem(clipboardGoogleDriveItemId);
                    isCutOperation = true;
                   
                }
                else
                {
                    // Локальний файл чи папка
                    clipboardPath = Path.Combine(currentFolderPath, selectedItem.Name);
                    isCutOperation = true;
                    
                }
            }
            else
            {
                MessageBox.Show("Виберіть файл або папку для вирізання.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedItem is FileItem selectedItem)
            {
                if (IsGoogleDrive) // Перевірка активної вкладки
                {
                    // Використовуємо Google Drive для вирізування
                    clipboardGoogleDriveItemId = selectedItem.Id; // Сохраняем ID элемента
                    _googleDriveMenuFunctions.CopyItem(clipboardGoogleDriveItemId);
                    isCutOperation = false;
                   
                }
                else
                {
                    // Локальний файл чи папка
                    clipboardPath = Path.Combine(currentFolderPath, selectedItem.Name);
                    isCutOperation = false;
                    
                }
            }
            else
            {
                MessageBox.Show("Виберіть файл або папку для копіювання.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

     
        private async void OnPasteClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Перевірка активної вкладки
                if (IsGoogleDrive)
                {
                    // Якщо джерело – локальний файл, а мета – Google Drive
                    if (!string.IsNullOrWhiteSpace(clipboardPath))
                    {
                        string fileName = Path.GetFileName(clipboardPath);

                        // Завантажуємо файл на Google Drive
                        if (Directory.Exists(clipboardPath))
                        {
                            await _googleDriveMenuFunctions.UploadFolderToGoogleDrive(clipboardPath, currentGoogleFolderId);
                            // Якщо операція вирізування, видаляємо локальньну папку
                            if (isCutOperation)
                            {
                                _menuFunctions.DeleteItem(clipboardPath);
                            }
                        }
                        else if (File.Exists(clipboardPath))
                        {
                            await _googleDriveMenuFunctions.UploadFileToGoogleDrive(clipboardPath, currentGoogleFolderId);
                            // Якщо операція вирізування, видаляємо локальний файл
                            if (isCutOperation)
                            {
                                _menuFunctions.DeleteItem(clipboardPath);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Шлях не існує або недоступний.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        clipboardPath = null; // Очищуємо буфер обміну
                        ShowGoogleDriveFolderContent(currentGoogleFolderId); // Оновлюємо вміст Google Drive
                    }
                    else if (!string.IsNullOrWhiteSpace(clipboardGoogleDriveItemId))
                    {

                        // Вставка всередині Google Drive (копіювання або переміщення)
                        await _googleDriveMenuFunctions.PasteItem(currentGoogleFolderId);
                        clipboardGoogleDriveItemId = null; // Очищаємо буфер обміну в гугл драйв
                        ShowGoogleDriveFolderContent(currentGoogleFolderId); // Обновляем содержимое
                    }
                    else
                    {
                        MessageBox.Show("Буфер обміну порожній. Спочатку виконайте вирізання або копіювання.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // Якщо джерело – Google Drive, а мета – локальний шлях
                    if (!string.IsNullOrWhiteSpace(clipboardGoogleDriveItemId))
                    {


                        // Завантажуємо файл із Google Drive
                        DriveService driveServicetemp = _googleDriveService.GetDriveService();
                        var request = driveServicetemp.Files.Get(clipboardGoogleDriveItemId);
                        request.Fields = "id, name, mimeType";
                        var fileInfo = await request.ExecuteAsync();
                        if (fileInfo.MimeType == "application/vnd.google-apps.folder")
                        {
                            string folderPath = Path.Combine(currentFolderPath, fileInfo.Name);
                            await _googleDriveMenuFunctions.DownloadFolderFromGoogleDrive(clipboardGoogleDriveItemId, folderPath);
                            await ShowFolderContent(currentFolderPath);
                            if (isCutOperation)
                            {
                               
                                await _googleDriveMenuFunctions.DeleteItem(clipboardGoogleDriveItemId);
                            }
                        }
                        else 
                        {   
                            string googleFilePath = Path.Combine(currentFolderPath, fileInfo.Name);
                            await _googleDriveMenuFunctions.DownloadFileFromGoogleDrive(fileInfo.Id, googleFilePath);
                             await  ShowFolderContent(currentFolderPath);
                            if (isCutOperation)
                            {
                               
                                await _googleDriveMenuFunctions.DeleteItem(clipboardGoogleDriveItemId);

                            }
                        }
                        clipboardGoogleDriveItemId = null;// Очищаємо буфер

                        
                    }
                    else if (!string.IsNullOrWhiteSpace(clipboardPath))
                    {

                        // Вставка всередині локальної системи (копіювання чи переміщення)
                        string destinationPath = Path.Combine(currentFolderPath, Path.GetFileName(clipboardPath));

                        _menuFunctions.PasteItem(clipboardPath, destinationPath, isCutOperation);

                        clipboardPath = null; // Очищаємо буфер
                        await ShowFolderContent(currentFolderPath);  // Оновлюємо вміст локальної папки

                    }
                    else
                    {
                        MessageBox.Show("Буфер обміну порожній. Спочатку виконайте вирізання або копіювання", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при вставленні {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedItem is FileItem selectedItem)
            {
                if (IsGoogleDrive) // Перевірка активної вкладки
                {
                    try
                    {
                        
                        string itemId = selectedItem.Id;// ID елемента видалення
                        await _googleDriveMenuFunctions.DeleteItemWithConfirmation(itemId);
                        ShowGoogleDriveFolderContent(currentGoogleFolderId);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка: {ex.Message}");
                    }
                }
                else
                {
                    if (selectedItem != null)
                    {
                        string selectedPath = Path.Combine(currentFolderPath, selectedItem.Name);

                        var result = MessageBox.Show(
                            $"Ви впевнені, що хочете видалити \"{selectedItem.Name}\"?",
                            "Підтвердження видалення",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning
                            );

                        if (result == MessageBoxResult.Yes)
                        {
                            _menuFunctions.DeleteItem(selectedPath);
                            if (forwardStack.Contains(selectedPath))
                            {
                                // Очищаємо стек вперед
                                forwardStack.Clear();
                            }
                            await ShowFolderContent(currentFolderPath);
                        }
                        
                        
                    }
                }
            }
            else
            {
                MessageBox.Show("Будь ласка, виберіть елемент для видалення.");
            }
        }
      
        private async void OnRenameClick(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedItem is FileItem selectedItem)
            {
                if (IsGoogleDrive)// Перевірка активної вкладки
                {
                    string newName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Уведіть нове ім'я:",
                    "Перейменування",
                    selectedItem.Name);
                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        try
                        {

                            // Асинхронне перейменування елемента в Google Drive
                            await _googleDriveMenuFunctions.RenameItem(selectedItem.Id, newName);

                            // Оновлення вмісту папки після перейменування
                            ShowGoogleDriveFolderContent(currentGoogleFolderId);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Помилка при перейменуванні: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }

                else
                {

                    // Запит нового імені через MessageBox
                    string newName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Введите новое имя:",
                    "Переименование",
                    selectedItem.Name);
                    // Якщо нове ім'я введене та не порожнє
                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        string selectedPath = Path.Combine(currentFolderPath, selectedItem.Name);
                        _menuFunctions.RenameItem(selectedPath, newName);
                        await ShowFolderContent(currentFolderPath);
                    }
                }
            }
            else
            {
                MessageBox.Show("Виберіть файл для перейменування.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string InputBox(string basicName)
        {
            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Уведіть нове ім'я:","Створення файлу",basicName);
            return newName;
        }
    

        private async void OnCreateFolderClick(object sender, RoutedEventArgs e)
        {
            if (IsGoogleDrive) // Перевірка активної вкладки
            {   
                    
                await  _googleDriveMenuFunctions.CreateItem("Folder", currentGoogleFolderId, InputBox("Нова папка"));
                ShowGoogleDriveFolderContent(currentGoogleFolderId);
            }

            else
            {
                _menuFunctions.CreateItem("Folder", currentFolderPath, InputBox("Нова папка"));
                await ShowFolderContent(currentFolderPath);
            }
        }

        private async void OnCreateTxtClick(object sender, RoutedEventArgs e)
        {
            if (IsGoogleDrive) // Перевірка активної вкладки
            {
                await _googleDriveMenuFunctions.CreateItem("TextFile", currentGoogleFolderId, InputBox("Новий текстовий документ"));
                ShowGoogleDriveFolderContent(currentGoogleFolderId);
            }
            else
            {
                _menuFunctions.CreateItem("Txt", currentFolderPath, InputBox("Новий текстовий документ"));
                await ShowFolderContent(currentFolderPath);
            }
        }


        private async void OnCreateWordClick(object sender, RoutedEventArgs e)
        {
            if (IsGoogleDrive) // Перевірка активної вкладки
            {
                await _googleDriveMenuFunctions.CreateItem("GoogleDoc", currentGoogleFolderId, InputBox("Новий Google Doc документ"));
                ShowGoogleDriveFolderContent(currentGoogleFolderId);
            }
            else
            {
                _menuFunctions.CreateItem("Word", currentFolderPath, InputBox("Microsoft Word Document"));

                await ShowFolderContent(currentFolderPath);
            }
        }

        private async void OnCreateExcelClick(object sender, RoutedEventArgs e)
        {
            if (IsGoogleDrive) // Перевірка активної вкладки
            {
                 await _googleDriveMenuFunctions.CreateItem("GoogleSheet", currentGoogleFolderId, InputBox("Новий Google Sheets документ"));
                ShowGoogleDriveFolderContent(currentGoogleFolderId);
            }
            else
            {
                _menuFunctions.CreateItem("Excel", currentFolderPath, InputBox("Microsoft PowerPoint Presentation"));
                await ShowFolderContent(currentFolderPath);
            }
        }

        private async void OnCreatePptClick(object sender, RoutedEventArgs e)
        {
            if (IsGoogleDrive) // Перевірка активної вкладки
            {
                 await  _googleDriveMenuFunctions.CreateItem("GoogleSlide", currentGoogleFolderId, InputBox("Нова GoogleSlide презентація"));
                ShowGoogleDriveFolderContent(currentGoogleFolderId);
            }
            else
            {
                _menuFunctions.CreateItem("Ppt", currentFolderPath, InputBox("Microsoft Excel Worksheet"));
                await  ShowFolderContent(currentFolderPath);
            }
        }
        private async void OnLocalDisksClick(object sender, RoutedEventArgs e)
        {
            await CancelTask(_cancelSearchTokenSource);
            ShowDrives();


            // Завантаження даних для локальних дисків
            IsGoogleDrive = false;
            if (isDiskStyle)
            {
                SearchTextBox.IsEnabled = false;
            }
            await ShowFolderContent(currentFolderPath);
            LocalDisksTab.Background = Brushes.LightBlue;
            GoogleDriveTab.Background = Brushes.Transparent;
            SetButtonsStates();
        }

        private async void OnGoogleDriveClick(object sender, RoutedEventArgs e)
        {
            // Завантаження даних для Google Drive
            await CancelTask(_cancelSearchTokenSource);
            ShowGoogleDriveFolderContent(currentGoogleFolderId);
            makeActiveButton(ListButton);
            makeActiveButton(TileButton);
            IsGoogleDrive = true;
            SearchTextBox.IsEnabled = true;
            _googleDriveService.FillGoogleDriveTreeView(treeView);
            GoogleDriveTab.Background = Brushes.LightBlue;
            LocalDisksTab.Background = Brushes.Transparent;
            SetButtonsStates();
            
        }
        private async void ShowGoogleDriveFolderContent(string folderId)
        {
            await CancelTask(_cancelSearchTokenSource);
            currentGoogleFolderId = folderId;
            makeInnactiveButton(HomeButton);
            fileListView.ItemsSource = null;
            UpdateBreadcrumbs(folderId, true);
            await _googleDriveService.FillGoogleDriveListView(fileListView, folderId);
            
            if (googleDriveForwardStack.Count == 0)
            {
                makeInnactiveButton(NextButton);
            }
            if(googleDriveBackStack.Count == 0)
            {
                makeInnactiveButton(BackButton);
            }
            ChooseStyle(currentStyle);

        }
        private void SetButtonsStates()
        {
            bool tempBackButtonState = BackButton.IsEnabled;
            bool tempForwardButtonState = NextButton.IsEnabled;
            if(backButtonState == false)
            {
                makeInnactiveButton(BackButton);
            }
            else
            {
                makeActiveButton(BackButton);
            }
            if (forwardButtonState == false)
            {
                makeInnactiveButton(NextButton);
            }
            else
            {
                makeActiveButton(NextButton);
            }
            backButtonState = tempBackButtonState;
            forwardButtonState = tempForwardButtonState;
        }
        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.Trim();
            // Скасовуємо попередню операцію, якщо вона ще не завершилась
            _debounceTokenSource?.Cancel();
            // Створюємо новий `CancellationTokenSource` для нової операції
            _debounceTokenSource = new CancellationTokenSource();
            var token = _debounceTokenSource.Token;

            try
            {
                // Затримка перед виконанням пошуку (500 мс)
                await Task.Delay(500, token);

                // Якщо токен не скасований, виконуємо пошук
                await PerformSearch(searchText);
            }
            catch (TaskCanceledException)
            {
                // Ігноруємо скасовані операції
            }


        }
        private async Task CancelTask(CancellationTokenSource cts)
        {
            cts.Cancel();
            await Task.Delay(200);
        }
        private async Task PerformSearch(string searchText)
        {

            // Скасуємо попередній пошук
            await CancelTask(_cancelSearchTokenSource);
            _cancelSearchTokenSource = new CancellationTokenSource();  // Новий токен скасування
            fileListView.ItemsSource = null;
             fileListView.Items.Clear();
            
             if (_menuFunctions == null || _googleDriveService == null)
             {
                 MessageBox.Show("Сервіси не ініціалізовні. перевірте налаштування.");
                 return;
             }

             if (string.IsNullOrEmpty(searchText))
             {
                 fileListView.ItemsSource = null;
                 fileListView.Items.Clear();
                 if (!IsGoogleDrive)
                 {
                     await ShowFolderContent(currentFolderPath);
                 }
                 else ShowGoogleDriveFolderContent(currentGoogleFolderId);
                 return;
             }
             else
             {
                 List<FileItem> results = new List<FileItem>();

                 try
                 {

                 
                    var token = _cancelSearchTokenSource.Token;
                    if (IsGoogleDrive)
                    {
                        // Пошук на Google Диску
                        await _googleDriveService.SearchGoogleDriveRecursive(
                            searchText,
                            currentGoogleFolderId,
                            fileItem =>
                            {
                                if (!token.IsCancellationRequested)
                                {
                                    Dispatcher.Invoke(() => fileListView.Items.Add(fileItem));
                                }
                            },
                            token);
                    }
                    else
                    {
                        // Локальний пошук
                        await Task.Run(() =>
                        {
                            _menuFunctions.SearchLocalFiles(
                                currentFolderPath,
                                searchText,
                                fileItem =>
                                {
                                    if (!token.IsCancellationRequested)
                                    {
                                        Dispatcher.Invoke(() => fileListView.Items.Add(fileItem));
                                    }
                                },
                                token);
                        }, token);
                    }




                }
                 catch (Exception ex)
                 {
                     MessageBox.Show($"Помилка пошуку: {ex.Message}");
                 }
             }
           
            
        }
        
        private async void UpdateBreadcrumbs(string currentPath, bool isGoogleDrive)
        {

            BreadcrumbPanel.Children.Clear();

            if (isGoogleDrive)
            {

                // Отримуємо хлібні крихти для Google Drive
                List<BreadcrumbItem> googleDriveBreadcrumbs = await _googleDriveService.GetGoogleDriveBreadcrumbs(currentPath);
                BreadcrumbPanel.Children.Clear();
                foreach (var breadcrumb in googleDriveBreadcrumbs)
                {
                    Button breadcrumbButton = new Button
                    {
                        Content = breadcrumb.Name,
                        Tag = breadcrumb,
                        Margin = new Thickness(0, 0, 5, 0),
                        Padding = new Thickness(5, 0, 5, 0),
                        BorderThickness = new Thickness(0),
                        Background = Brushes.Transparent
                    };

                    breadcrumbButton.Click += BreadcrumbButton_Click;
                    BreadcrumbPanel.Children.Add(breadcrumbButton);

                    if (breadcrumb != googleDriveBreadcrumbs[googleDriveBreadcrumbs.Count - 1])
                    {
                        TextBlock arrow = new TextBlock
                        {
                            Text = ">",
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(2, 0, 2, 0),
                            FontSize = 14,
                            Foreground = Brushes.Black
                        };
                        BreadcrumbPanel.Children.Add(arrow);
                    }
                }
            }
            else
            {
                // Звичайна обробка для локальних папок
                string[] parts;

                // Розділяємо шлях по ":"
                if (currentPath.Contains(":"))
                {
                    parts = currentPath.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    parts[0] = parts[0] + ":";  // Додаємо ":" назад до диску

                }
                else
                {
                    parts = new[] { currentPath }; // У разі, якщо роздільника ":" немає
                }

                // Додаємо перший елемент (диск) вручну
                string accumulatedPath = parts[0];
                Button diskButton = new Button
                {
                    Content = accumulatedPath,
                    Tag = new BreadcrumbItem
                    {
                        Name = accumulatedPath,
                        FullPath = accumulatedPath,
                        IsGoogleDrive = isGoogleDrive
                    },
                    Margin = new Thickness(0, 0, 5, 0),
                    Padding = new Thickness(5, 0, 5, 0),
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent
                };
                diskButton.Click += BreadcrumbButton_Click;
                BreadcrumbPanel.Children.Add(diskButton);

                // Додаємо стрілку після диска, якщо є подальші частини шляху
                if (parts.Length > 1)
                {
                    TextBlock arrow = new TextBlock
                    {
                        Text = ">",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(2, 0, 2, 0),
                        FontSize = 14,
                        Foreground = Brushes.Black
                    };
                    BreadcrumbPanel.Children.Add(arrow);
                }

                // Обробляємо частину шляху, що залишилася.
                for (int i = 1; i < parts.Length; i++) // Починаємо з індексу 1
                {
                    string[] subParts = parts[i].Split(new[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string part in subParts)
                    {
                        accumulatedPath = Path.Combine(accumulatedPath, part);

                        Button breadcrumbButton = new Button
                        {
                            Content = part,
                            Tag = new BreadcrumbItem
                            {
                                Name = part,
                                FullPath = accumulatedPath,
                                IsGoogleDrive = isGoogleDrive
                            },
                            Margin = new Thickness(0, 0, 5, 0),
                            Padding = new Thickness(5, 0, 5, 0),
                            BorderThickness = new Thickness(0),
                            Background = Brushes.Transparent
                        };

                        breadcrumbButton.Click += BreadcrumbButton_Click;
                        BreadcrumbPanel.Children.Add(breadcrumbButton);


                        // Додаємо стрілку після кожного елемента, крім останнього
                        if (part != subParts.Last() || i != parts.Length - 1)
                        {
                            TextBlock arrow = new TextBlock
                            {
                                Text = ">",
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = new Thickness(2, 0, 2, 0),
                                FontSize = 14,
                                Foreground = Brushes.Black
                            };
                            BreadcrumbPanel.Children.Add(arrow);
                        }
                    }
                }
            }
        }

        private async void BreadcrumbButton_Click(object sender, RoutedEventArgs e)
        {   
            if (sender is Button button && button.Tag is BreadcrumbItem item)
            {
                await CancelTask(_cancelSearchTokenSource);

                // Переходимо до обраної папки


                if (item.IsGoogleDrive)
                {
                    // Оновлюємо вміст для Google Drive
                    ShowGoogleDriveFolderContent(item.FullPath);
                }
                else
                {
                    currentFolderPath = item.FullPath;

                    // Оновлюємо вміст для локальної системи
                    await ShowFolderContent(item.FullPath);
                }
                // Оновлюємо хлібні крихти
                UpdateBreadcrumbs(item.FullPath, item.IsGoogleDrive);
            }
        }
        
    }


}


