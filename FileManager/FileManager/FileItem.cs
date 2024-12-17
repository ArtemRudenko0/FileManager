using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FileManager
{
    public class FileItem
    {
        public string Name { get; set; } // Ім'я файлу/папки
        public string Type { get; set; } // Тип: файл або папка
        public string DateModified { get; set; } // Дата зміни
        public string Id { get; set; }          // ID для Google Drive
        public string Size { get; set; } // Розмір у зручному форматі
        public string Icon { get; set; } // Шлях до іконки

        public string FullPath { get; set; } // Для локальних файлів
        public long TotalSize { get; set; } //Загальний розщір(Для диску)
        public long FreeSpace { get; set; } //Доступно місця(Для диску)

    }
}
    
