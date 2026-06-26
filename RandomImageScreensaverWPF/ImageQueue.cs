using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using System.Timers;

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

        private readonly Channel<string> _directoryChannel;
        private readonly ConcurrentBag<string> _concurrentImageStore;
        private readonly System.Timers.Timer _synchronizer;
        private readonly List<string> _imageList;
        private readonly Task[] _workers;
        private readonly int _maxHistoryEntries;
        private readonly int _marginsSize;
        private readonly LinkedList<ImageNode> _history = new();
        private LinkedListNode<ImageNode>? _current = null;

        public bool IsScanning { get => _synchronizer.Enabled; }
        public bool IsEmpty { get => _imageList.Count == 0; }


        private ImageQueue(int maxHistory, int marginsSize = 0)
        {
            _maxHistoryEntries = Math.Max(1, maxHistory);
            _directoryChannel = Channel.CreateBounded<string>(100);
            _synchronizer = new(5000);
            _synchronizer.Elapsed += SyncImageList;
            _synchronizer.AutoReset = true;

            _concurrentImageStore = [];
            _imageList = [];
            _workers = new Task[3];
            
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
            queue._directoryChannel.Writer.TryWrite(directory);
            queue._synchronizer.Start();

            for (int i = 0; i < queue._workers.Length; i++)
            {
                int workerId = i + 1;
                queue._workers[i] = Task.Run(() => queue.StartWorkAsync(workerId, directory));
            }
            
            return queue;
        }

        private async void StartWorkAsync(int id, string root)
        {
            Debug.WriteLine($"Worker {id} started.");

            try
            {
                // ReadAllAsync natively blocks until an item arrives, or exits when Channel is Complete
                await foreach (var job in _directoryChannel.Reader.ReadAllAsync())
                {
                    Debug.WriteLine($"Worker {id} is processing: {job}");
                    HandleDirectory(job);
                }
            }
            catch (OperationCanceledException)
            {
            }

            Debug.WriteLine($"Worker {id} shut down.");
        }

        private void SyncImageList(object? sender, ElapsedEventArgs e)
        {
            Debug.WriteLine("Syncing image list");

            int migrates = 0;
            while (_concurrentImageStore.TryTake(out var result))
            {
                if (result is not null)
                {
                    migrates++;
                    _imageList.Add(result);
                }
            }
            Debug.WriteLine($"Moved {migrates} images");


            if (_directoryChannel.Reader.Count == 0)
            {
                Debug.WriteLine("No more scanning left, halting workers");

                _directoryChannel.Writer.TryComplete();

                if (sender is System.Timers.Timer currentTimer)
                {
                    currentTimer.Stop();
                    currentTimer.Dispose();
                }
            }
        }

        private async void HandleDirectory(string dir)
        {
            try
            {
                var images = Directory.EnumerateFiles(dir, searchPattern: "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => _imageExtensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()));

                foreach (var image in images)
                {
                    _concurrentImageStore.Add(image);
                }

                foreach (string directory in Directory.EnumerateDirectories(dir).Shuffle())
                {
                    await _directoryChannel.Writer.WriteAsync(directory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while loading images: {ex.Message}");
            }
        }

        public string MoveNext()
        {
            if (IsEmpty)
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
            if (avoidIndex == -1)
            {
                avoidIndex = _current != null ? _current.Value.Index : -1;
            }

            int newIndex;
            do
            {
                newIndex = _random.Next(_imageList.Count);
            } while (_imageList.Count > 1 && newIndex == avoidIndex);

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
            _imageList.Remove(toRemove.Value.Path);
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
            int? lastIndex = isRightMargin ? _history.Last?.Value.Index : _history.First?.Value.Index;
            foreach (ImageNode node in GenerateNodes(diff, lastIndex ?? -1)) 
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
                int nextImageIndex = GetRandomImageIndex(avoidIndex: lastIndex);
                nodes[i] = new(nextImageIndex, _imageList[nextImageIndex]);
                lastIndex = nextImageIndex;
            }

            return nodes;
        }
    }
}
