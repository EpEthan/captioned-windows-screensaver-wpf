using System.IO;

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
        private readonly int _maxHistoryEntries;
        private readonly int _marginsSize;
        private readonly LinkedList<ImageNode> _history = new();
        private LinkedListNode<ImageNode>? _current = null;

        private volatile int _runningJobCount = 0;
        public bool IsScanning => _runningJobCount > 0;

        public ImageQueue(int maxHistory, List<string>? files = null, int marginsSize = 0)
        {
            _maxHistoryEntries = Math.Max(1, maxHistory);
            _files = files is not null ? files : new([]);
            
            marginsSize = Math.Max(0, marginsSize);
            _marginsSize = (_maxHistoryEntries - 2 * marginsSize) > 0 ? marginsSize : 0;
        }

        public static ImageQueue LoadFromDirectoryAsync(string directory, int maxHistory, int marginsSize = 0)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(directory);
            }

            ImageQueue queue = new(maxHistory, marginsSize: marginsSize);
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

            ImageNode node = MoveOrGenerateNext();

            FillMargins();
            if (_history.Count > _maxHistoryEntries)
            {
                _history.RemoveFirst();
            }

            return node.Path;
        }

        private ImageNode MoveOrGenerateNext()
        {
            if (_current != null && _current.Next != null)
            {
                _current = _current.Next;
                return _current.Value;
            }

            ImageNode newNode = GenerateNode();
            if (_current == null)
            {
                _history.AddFirst(newNode);
                _current = _history.First;
            }
            else
            {
                _history.AddLast(newNode);
                _current = _history.Last;
            }
            return newNode;
        }

        private int GetRandomImageIndex(int avoidIndex = -1)
        {
            if (avoidIndex != -1)
            {
                avoidIndex = _current != null ? _current.Value.Index : -1;
            }

            int newIndex;
            do
            {
                newIndex = _random.Next(_files.Count);
            } while (_files.Count > 1 && newIndex == avoidIndex);

            return newIndex;
        }

        public string MoveBack() 
        {
            ImageNode node = MoveOrGenerateBack();

            FillMargins();
            if (_history.Count > _maxHistoryEntries)
            {
                _history.RemoveLast();
            }

            return node.Path;
        }

        private ImageNode MoveOrGenerateBack()
        {
            if (_current == null) return MoveOrGenerateNext();

            if (_current.Previous != null)
            {
                _current = _current.Previous;
                return _current.Value;
            }

            ImageNode newNode = GenerateNode();
            _history.AddFirst(newNode);
            _current = _history.First;
            return newNode;
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
        
        private void FillMargins()
        {
            FillMargin(true);
            FillMargin(false);
        }

        private void FillMargin(bool isRightMargin)
        {
            if (_marginsSize <= 0) return;

            int currentMarginSize = Peek(isRightMargin ? _marginsSize : -1 * _marginsSize).Length;
            if (currentMarginSize >= _marginsSize) return;

            int diff = _marginsSize - currentMarginSize;
            foreach (ImageNode node in GenerateNodes(diff)) 
            {
                if (isRightMargin)
                {
                    _history.AddLast(node);
                }
                else
                {
                    _history.AddFirst(node);
                }
            }            
        }

        public string[] Peek(int count)
        {
            if (_current == null || count == 0)
            {
                return [];
            }

            List<string> output = new(Math.Abs(count));
            LinkedListNode<ImageNode> current = _current;
            for (int i = 0; i < Math.Abs(count); i++)
            {
                LinkedListNode<ImageNode>? next = count > 0 ? current.Next : current.Previous;
                
                if (next == null) break;

                output.Add(next.Value.Path);
                current = next;
            }

            return [.. output];
        }

        private ImageNode GenerateNode(int avoidIndex = -1)
        {
            return GenerateNodes(1, avoidIndex)[0];
        }

        private ImageNode[] GenerateNodes(int count, int avoidIndex = -1)
        {
            ImageNode[] nodes = new ImageNode[count];

            int lastIndex = avoidIndex;
            for (int i = 0; i < count; i++)
            {
                int nextImageIndex = GetRandomImageIndex(lastIndex);
                nodes[i] = new(nextImageIndex, _files[nextImageIndex]);
                lastIndex = nextImageIndex;
            }

            return nodes;
        }
    }
}
