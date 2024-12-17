using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileManager
{
    internal class BreadcrumbItem
    {
        public string Name { get; set; }        // Ім'я папки 
        public string FullPath { get; set; }   // Повний шлях до цього елемента
        public bool IsGoogleDrive { get; set; } //Прапор, чи відноситься елемент до Google Диску
    }
}
