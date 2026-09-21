using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TextAnalysisApp
{
    class Program
    {
        // Список слов для поиска (можно изменить)
        static readonly List<string> wordsToSearch = new List<string>
        {
            "the", "and", "of", "to", "a", "in", "is", "that", "it", "for",
            "was", "on", "are", "as", "with", "his", "they", "at", "be", "this",
            "he", "or", "had", "by", "not", "but", "what", "all", "were", "when",
            "from", "have", "him", "which", "she", "do", "so", "said", "each", "into"
        };

        static void Main(string[] args)
        {
            string folderPath = "new_data";

            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine($"Папка {folderPath} не найдена!");
                return;
            }

            var txtFiles = Directory.GetFiles(folderPath, "*.txt").ToList();
            
            if (txtFiles.Count == 0)
            {
                Console.WriteLine("Файлы .txt не найдены!");
                return;
            }

            Console.WriteLine($"Найдено файлов: {txtFiles.Count}");
            Console.WriteLine($"Слов для поиска: {wordsToSearch.Count}");
            Console.WriteLine(new string('-', 60));

            // Последовательная обработка
            Console.WriteLine("\n=== ПОСЛЕДОВАТЕЛЬНАЯ ОБРАБОТКА ===");
            var sequentialResult = SequentialProcessing(txtFiles, wordsToSearch);
            PrintResults(sequentialResult, "Последовательный метод");

            // Параллельная обработка (MapReduce с Parallel и ConcurrentDictionary)
            Console.WriteLine("\n=== ПАРАЛЛЕЛЬНАЯ ОБРАБОТКА (MapReduce) ===");
            var parallelResult = ParallelMapReduceProcessing(txtFiles, wordsToSearch);
            PrintResults(parallelResult, "Параллельный метод (MapReduce)");

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        /// <summary>
        /// Последовательная обработка файлов с использованием foreach + Dictionary
        /// </summary>
        static Dictionary<string, long> SequentialProcessing(List<string> files, List<string> words)
        {
            var stopwatch = Stopwatch.StartNew();
            var wordCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                // Потоковое чтение файла построчно (не загружаем весь файл в память)
                foreach (var line in File.ReadLines(file))
                {
                    // Предобработка текста: приводим к нижнему регистру, оставляем только буквы и пробелы
                    var processedLine = PreprocessText(line);
                    
                    // Подсчет вхождений каждого слова
                    foreach (var word in words)
                    {
                        var lowerWord = word.ToLower();
                        if (!wordCounts.ContainsKey(lowerWord))
                            wordCounts[lowerWord] = 0;
                        
                        // Считаем вхождения слова в строке
                        wordCounts[lowerWord] += CountWordOccurrences(processedLine, lowerWord);
                    }
                }
            }

            stopwatch.Stop();
            Console.WriteLine($"Время выполнения: {stopwatch.ElapsedMilliseconds} мс");

            return wordCounts;
        }

        /// <summary>
        /// Параллельная обработка с использованием алгоритма MapReduce
        /// Map: Разбиваем тексты на части и параллельно подсчитываем вхождения слов
        /// Reduce: Объединяем результаты из всех частей
        /// </summary>
        static Dictionary<string, long> ParallelMapReduceProcessing(List<string> files, List<string> words)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Потокобезопасная коллекция для хранения результатов
            var concurrentCounts = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            // Параллельная обработка файлов с помощью Parallel.ForEach
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
            {
                // Локальный словарь для каждого файла (этап Map)
                var localCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

                // Потоковое чтение файла построчно
                foreach (var line in File.ReadLines(file))
                {
                    var processedLine = PreprocessText(line);
                    
                    foreach (var word in words)
                    {
                        var lowerWord = word.ToLower();
                        if (!localCounts.ContainsKey(lowerWord))
                            localCounts[lowerWord] = 0;
                        
                        localCounts[lowerWord] += CountWordOccurrences(processedLine, lowerWord);
                    }
                }

                // Этап Reduce: объединяем локальные результаты в общую коллекцию
                foreach (var kvp in localCounts)
                {
                    concurrentCounts.AddOrUpdate(kvp.Key, kvp.Value, (key, oldValue) => oldValue + kvp.Value);
                }
            });

            stopwatch.Stop();
            Console.WriteLine($"Время выполнения: {stopwatch.ElapsedMilliseconds} мс");

            return concurrentCounts.ToDictionary(k => k.Key, v => v.Value);
        }

        /// <summary>
        /// Предобработка текста: приведение к нижнему регистру, удаление всех символов кроме букв и пробелов
        /// </summary>
        static string PreprocessText(string text)
        {
            var result = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsLetter(c) || char.IsWhiteSpace(c))
                    result.Append(char.ToLower(c));
                else if (!char.IsLetterOrDigit(c))
                    result.Append(' '); // Заменяем пунктуацию на пробел
            }
            return result.ToString();
        }

        /// <summary>
        /// Подсчет количества вхождений слова в тексте (как отдельных слов)
        /// </summary>
        static int CountWordOccurrences(string text, string word)
        {
            int count = 0;
            int startIndex = 0;
            
            while ((startIndex = text.IndexOf(word, startIndex, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                // Проверяем, что слово является отдельным (границы слова)
                bool isStartBoundary = startIndex == 0 || !char.IsLetter(text[startIndex - 1]);
                bool isEndBoundary = startIndex + word.Length >= text.Length || 
                                     !char.IsLetter(text[startIndex + word.Length]);
                
                if (isStartBoundary && isEndBoundary)
                    count++;
                
                startIndex++;
            }
            
            return count;
        }

        /// <summary>
        /// Вывод ранжированной статистики (от максимального к минимальному)
        /// </summary>
        static void PrintResults(Dictionary<string, long> results, string methodName)
        {
            Console.WriteLine($"\n--- Результаты: {methodName} ---");
            Console.WriteLine(new string('-', 40));
            
            var sortedResults = results.OrderByDescending(kvp => kvp.Value).Take(20);
            
            Console.WriteLine($"{"Слово",-15} {"Вхождений",-10}");
            Console.WriteLine(new string('-', 40));
            
            foreach (var kvp in sortedResults)
            {
                Console.WriteLine($"{kvp.Key,-15} {kvp.Value,-10}");
            }
            
            Console.WriteLine(new string('-', 40));
            Console.WriteLine($"Всего уникальных слов: {results.Count}");
        }
    }
}
