using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace RandomImageScreensaverWPF
{
    internal class ImageQueue
    {
        private class ImageNode
        {
            public int Index { get; }
            public string Path { get; }

            public ImageNode(int index, string path)
            {
                Index = index;
                Path = path;
            }
        }

        private static readonly string[] _imageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];
        private readonly Random _random = new();

        private readonly List<string> _files;
        private readonly LinkedList<ImageNode> _history = new();
        private LinkedListNode<ImageNode>? _current = null;

        private volatile int _runningJobCount = 0;
        public bool IsScanning => _runningJobCount > 0;

        public ImageQueue(List<string>? files = null)
        {
            _files = files is not null ? files : new([]);
        }

        public static ImageQueue LoadFromDirectoryAsync(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(directory);
            }

            ImageQueue queue = new();
            Task.Run(() => queue.ScanRecursivelyInBackground(SettingsManager.ImageDirectoryPath));
            return queue;
        }

        private void ScanRecursivelyInBackground(string root)
        {
            _runningJobCount++;

            try
            {
                var images = Directory.EnumerateFiles(root, searchPattern: "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => _imageExtensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()));
                _files.AddRange(images);

                foreach (string directory in Directory.EnumerateDirectories(root))
                {
                    Task.Run(() => { ScanRecursivelyInBackground(directory); });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while loading images: {ex.Message}");
            }
            finally
            {
                _runningJobCount--;
            }
        }

        public bool IsEmpty => _files.Count == 0;

        public string MoveNext()
        {
            if (_files.Count == 0)
            {
                throw new FileNotFoundException();
            }

            if (_current != null && _current.Next != null)
            {
                _current = _current.Next;
                return _current.Value.Path;
            }
            
            int nextImageIndex = GetNextRandomImageIndex();
            var newNode = new ImageNode(nextImageIndex, _files[nextImageIndex]);

            if (_current == null)
            {
                _history.AddFirst(newNode);
                _current = _history.First;
            } else
            {
                _history.AddLast(newNode);
                _current = _history.Last;
            }

            return newNode.Path;
        }

        private int GetNextRandomImageIndex()
        {
            int newIndex;
            do
            {
                newIndex = _random.Next(_files.Count);
            } while (_files.Count > 1 && _current != null && newIndex == _current.Value.Index);

            return newIndex;
        }

        public string? MoveBack() {
            if (_current == null || _current.Previous == null)
            {
                return null;
            }

            _current = _current.Previous;
            return _current.Value.Path;
        }

        public string? Current()
        {
            return _current?.Value.Path;
        }

        public void RemoveCurrent()
        {
            if (_current == null)
            {
                return;
            }

            var toRemove = _current;
            _current = _current.Previous;
            _history.Remove(toRemove);
        }
    }
}
