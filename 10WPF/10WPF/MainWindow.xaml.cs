using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace _10WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<CartItem> CartItems { get; set; }
        private DispatcherTimer timer;

        public MainWindow()
        {
            InitializeComponent();
            CartItems = new ObservableCollection<CartItem>();
            CartDataGrid.ItemsSource = CartItems;
            
            // Start timer for date/time display
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
            
            // Set initial date/time
            UpdateDateTime();
            
            // Subscribe to cart collection changes
            CartItems.CollectionChanged += CartItems_CollectionChanged;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateDateTime();
        }

        private void UpdateDateTime()
        {
            DateTimeText.Text = DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt");
        }

        private void CartItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateTotals();
        }

        private void BarcodeTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox.Text == "Enter barcode or product name..." && textBox.Foreground == Brushes.Gray)
            {
                textBox.Text = "";
                textBox.Foreground = Brushes.Black;
            }
        }

        private void BarcodeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "Enter barcode or product name...";
                textBox.Foreground = Brushes.Gray;
            }
        }

        private void BarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddToCart_Click(sender, e);
            }
        }

        private void AddToCart_Click(object sender, RoutedEventArgs e)
        {
            string searchText = BarcodeTextBox.Text;
            if (string.IsNullOrWhiteSpace(searchText) || searchText == "Enter barcode or product name...")
            {
                StatusText.Text = "Please enter a product name or barcode";
                return;
            }

            // Simulate product lookup (in real app, this would query a database)
            var product = LookupProduct(searchText);
            if (product != null)
            {
                AddItemToCart(product.Name, product.Price);
                BarcodeTextBox.Text = "";
                BarcodeTextBox.Focus();
                StatusText.Text = $"Added {product.Name} to cart";
            }
            else
            {
                StatusText.Text = "Product not found";
            }
        }

        private void ManualAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProductNameTextBox.Text) || ProductNameTextBox.Text == "Product Name")
            {
                StatusText.Text = "Please enter product name";
                return;
            }

            if (!decimal.TryParse(ProductPriceTextBox.Text.Replace("Price", ""), out decimal price) || price <= 0)
            {
                StatusText.Text = "Please enter valid price";
                return;
            }

            if (!int.TryParse(ProductQuantityTextBox.Text, out int quantity) || quantity <= 0)
            {
                StatusText.Text = "Please enter valid quantity";
                return;
            }

            for (int i = 0; i < quantity; i++)
            {
                AddItemToCart(ProductNameTextBox.Text, price);
            }

            // Clear fields
            ProductNameTextBox.Text = "Product Name";
            ProductPriceTextBox.Text = "Price";
            ProductQuantityTextBox.Text = "1";
            
            StatusText.Text = $"Added {quantity} x {ProductNameTextBox.Text} to cart";
        }

        private void QuickItem_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string[] itemInfo = button.Tag.ToString().Split('|');
            string name = itemInfo[0];
            decimal price = decimal.Parse(itemInfo[1]);
            
            AddItemToCart(name, price);
            StatusText.Text = $"Added {name} to cart";
        }

        private void AddItemToCart(string name, decimal price)
        {
            // Check if item already exists in cart
            var existingItem = CartItems.FirstOrDefault(item => item.Name == name && item.Price == price);
            if (existingItem != null)
            {
                existingItem.Quantity++;
            }
            else
            {
                CartItems.Add(new CartItem { Name = name, Price = price, Quantity = 1 });
            }
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            CartItem item = button.DataContext as CartItem;
            if (item != null)
            {
                CartItems.Remove(item);
                StatusText.Text = $"Removed {item.Name} from cart";
            }
        }

        private void ClearCart_Click(object sender, RoutedEventArgs e)
        {
            if (CartItems.Count > 0)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to clear the cart?", 
                    "Clear Cart", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    CartItems.Clear();
                    StatusText.Text = "Cart cleared";
                }
            }
        }

        private void PaymentMethod_Click(object sender, RoutedEventArgs e)
        {
            if (CartItems.Count == 0)
            {
                StatusText.Text = "Cart is empty";
                return;
            }

            Button button = sender as Button;
            string paymentMethod = button.Tag.ToString();
            decimal total = CartItems.Sum(item => item.Total);

            MessageBoxResult result = MessageBox.Show(
                $"Process payment of {total:C} via {paymentMethod}?", 
                "Confirm Payment", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Process payment (in real app, integrate with payment processor)
                ProcessPayment(paymentMethod, total);
            }
        }

        private void ProcessPayment(string method, decimal amount)
        {
            // Simulate payment processing
            StatusText.Text = $"Processing {method} payment of {amount:C}...";
            
            // In a real application, you would integrate with payment processors here
            
            MessageBox.Show($"Payment of {amount:C} processed successfully via {method}!\n\nThank you for your purchase!", 
                "Payment Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // Clear cart after successful payment
            CartItems.Clear();
            StatusText.Text = "Payment completed. Ready for next customer";
        }

        private void UpdateTotals()
        {
            decimal subtotal = CartItems.Sum(item => item.Total);
            decimal tax = subtotal * 0.08m; // 8% tax
            decimal total = subtotal + tax;

            SubtotalText.Text = subtotal.ToString("C");
            TaxText.Text = tax.ToString("C");
            TotalText.Text = total.ToString("C");
            
            ItemCountText.Text = CartItems.Sum(item => item.Quantity).ToString();
            StatusTotalText.Text = total.ToString("C");
        }

        private Product? LookupProduct(string searchText)
        {
            // Simulate product database lookup
            var products = new List<Product>
            {
                new Product { Name = "Milk", Price = 3.99m },
                new Product { Name = "Bread", Price = 2.50m },
                new Product { Name = "Eggs", Price = 4.99m },
                new Product { Name = "Banana", Price = 1.99m },
                new Product { Name = "Water", Price = 1.50m },
                new Product { Name = "Cheese", Price = 5.99m },
                new Product { Name = "Chicken", Price = 8.99m },
                new Product { Name = "Rice", Price = 3.49m },
                new Product { Name = "Pasta", Price = 1.99m },
                new Product { Name = "Tomato", Price = 2.99m }
            };

            return products.FirstOrDefault(p => 
                p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class CartItem : INotifyPropertyChanged
    {
        private int quantity;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        
        public int Quantity 
        { 
            get => quantity;
            set
            {
                quantity = value;
                OnPropertyChanged(nameof(Quantity));
                OnPropertyChanged(nameof(Total));
            }
        }
        
        public decimal Total => Price * Quantity;

        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Product
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}