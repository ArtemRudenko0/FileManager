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
        public string Name { get; set; } // Имя файла/папки
        public string Type { get; set; } // Тип: файл или папка
        public string DateModified { get; set; } // Дата изменения
        public string Size { get; set; } // Размер в удобном формате
        public string Icon { get; set; } // Путь к иконке

    }
}
    
