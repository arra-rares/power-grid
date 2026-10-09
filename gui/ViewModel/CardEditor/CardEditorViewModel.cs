using Microsoft.Win32;
using gui.Helpers;
using gui.Model.Managers.CardManager;
using gui.Model.Persistence;
using gui.Model.Managers.MarketManager;
using gui.Model.Utils;
using gui.View.CardEditor;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Serilog;

namespace gui.ViewModel.CardEditor
{
    public class CardEditorViewModel : ViewModelBase
    {
        public List<string> CardTypes { get; } = 
            ListUtils.EnumToList<CardType, string>(e => e.ToString());
        private string _imagePath = "";
        private string _cardId = "";
        private int _rank = 0;
        private bool _plus = false;
        private bool _endsTurn = false;
        private bool _bureaucrat = false;
        private bool _level3 = false;
        private int _marketEffectLowest = 0;
        private int _marketEffectHighest = 0;

        public List<CardResourceBtnViewModel> ResourceButtons { get; } = 
            ListUtils.EnumToList<ResourceType, CardResourceBtnViewModel>(type => new CardResourceBtnViewModel(type));

        private BitmapImage? _cardImage;

        public CardEditorViewModel()
        {
            _selectedCardType = CardTypes.FirstOrDefault() ?? "PowerPlant"; // Default to first value
            CardManager.Instance.UnknownCardScanned += OnUnknownCardScanned;
        }

        private string _selectedCardType;
        public string SelectedCardType
        {
            get => _selectedCardType;
            set => SetProperty(ref _selectedCardType, value);
        }

        public string CardId
        {
            get => _cardId;
            set => SetProperty(ref _cardId, value);
        }

        public int Rank
        {
            get => _rank;
            set => SetProperty(ref _rank, value);
        }

        public bool Plus
        {
            get => _plus;
            set => SetProperty(ref _plus, value);
        }

        public bool EndsTurn
        {
            get => _endsTurn;
            set => SetProperty(ref _endsTurn, value);
        }

        public bool Bureaucrat
        {
            get => _bureaucrat;
            set => SetProperty(ref _bureaucrat, value);
        }

        public bool Level3
        {
            get => _level3;
            set => SetProperty(ref _level3, value);
        }

        public string ImagePath
        {
            get => _imagePath;
            set
            {
                if (SetProperty(ref _imagePath, value))
                    CardImage = string.IsNullOrEmpty(_imagePath) ? null : new BitmapImage(new Uri(_imagePath));
            }
        }

        public BitmapImage? CardImage
        {
            get => _cardImage;
            set => SetProperty(ref _cardImage, value);
        }

        public List<int> MarketEffect { get; } = ListUtils.EnumToList<ResourceType, int>(_ => 0);

        public int MarketEffectLowest
        {
            get => _marketEffectLowest;
            set
            {
                _marketEffectLowest = value;
                OnPropertyChanged();
            }
        }

        public int MarketEffectHighest
        {
            get => _marketEffectHighest;
            set
            {
                _marketEffectHighest = value;
                OnPropertyChanged();
            }
        }

        public ICommand SelectImageCommand => new RelayCommand(_ => SelectImage());

        private void SelectImage()
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Title = "Select Card Image"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ImagePath = openFileDialog.FileName;
            }
        }

        private void OnUnknownCardScanned(int cardId)
        {
            if (GameSession.Active)
            {
                Log.Information("Card catalog editor is blocked while a session is active. Unknown card {CardId}", cardId);
                return;
            }

            Log.Information($"OnUnknownCardScanned, id {cardId}");

            Application.Current.Dispatcher.Invoke(() =>
            {
                CardId = cardId.ToString();
                var editor = new gui.View.CardEditor.CardEditorDialog { DataContext = this };

                if (editor.ShowDialog() == true)
                {
                    Console.WriteLine($"Card {CardId} saved. User must scan again.");
                    // Do nothing! The game will require a re-scan.
                }
            });
        }

        public ICommand CloseCommand => new RelayCommand(_ => CloseWindow());

        public ICommand SaveCommand => new RelayCommand(_ => SaveCard());

        private void CloseWindow()
        {
            if (Application.Current.Windows.OfType<CardEditorDialog>().FirstOrDefault() is CardEditorDialog window)
            {
                window.Close();
            }
        }

        private void SaveCard()
        {
            // Placeholder for actual save logic (e.g., updating database, saving to JSON, etc.)
            MessageBox.Show("Card saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            var imagePath = setImage();

            var newCard = new Card
            {
                Id = int.Parse(CardId),
                Type = Enum.Parse<CardType>(SelectedCardType),
                Rank = Rank,
                ImagePath = imagePath, // Store the image path in the card
                Plus = Plus,
                EndsTurn = EndsTurn,
                Bureaucrat = Bureaucrat,
                Level3 = Level3,
                MarketEffect = [.. MarketEffect],
                MarketEffectLowest = MarketEffectLowest,
                MarketEffectHighest = MarketEffectHighest,
                SupportedResources = ResourceButtons.Select(btn => btn.Filled).ToList()
            };

            // Add the card to CardManager
            CardManager.Instance.AddCard(newCard);

            // Close the window after saving
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is CardEditorDialog)?.Close();
        }

        private string setImage()
        {
            string cardsDirectory = "cards";
            if (!Directory.Exists(cardsDirectory))
            {
                Directory.CreateDirectory(cardsDirectory);
            }

            // Generate the PNG path
            string imagePath = Path.Combine(cardsDirectory, $"{CardId}.png");

            // If a new image was selected, save it as PNG
            if (!string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath))
            {
                ConvertToPng(ImagePath, imagePath);
            }

            return imagePath;
        }

        private void ConvertToPng(string inputPath, string outputPath)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage(new Uri(inputPath));
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error converting image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
