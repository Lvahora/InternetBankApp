using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using InternetBankApp.Models;
using InternetBankApp.Services;
using Microsoft.EntityFrameworkCore;

namespace InternetBankApp;

public partial class MainWindow : Window
{
    private BankDbContext _db = null!;
    private BankService _service = null!;
    private BankAccount? _selectedAccount;

    public MainWindow()
    {
        InitializeComponent();

        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseNpgsql("Host=localhost;Database=2laba;Username=postgres;Password=sa")
            .Options;

        _db = new BankDbContext(options);
        _db.Database.EnsureCreated();
        _service = new BankService(_db);

        TxtReceiver.Text = "Введите ID получателя";
        TxtReceiver.Foreground = Brushes.Gray;

        SeedData();
        LoadAccounts();
    }

    private void SeedData()
    {
        if (_db.Users.Any()) return;

        var user = new User { FullName = "Иван Иванов", Email = "ivan@mail.ru" };
        var acc1 = new BankAccount { AccountNumber = "RU10000001", Balance = 50000, Currency = "RUB", User = user };
        var acc2 = new BankAccount { AccountNumber = "RU20000002", Balance = 10000, Currency = "RUB", User = user };

        _db.Users.Add(user);
        _db.Accounts.Add(acc1);
        _db.Accounts.Add(acc2);
        _db.SaveChanges();
    }

    private void LoadAccounts()
    {
        var accounts = _db.Accounts.ToList();
        AccountsList.ItemsSource = null;
        AccountsList.ItemsSource = accounts;
        if (accounts.Any() && AccountsList.SelectedIndex == -1)
            AccountsList.SelectedIndex = 0;
    }

    private void AccountsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AccountsList.SelectedItem is not BankAccount acc) return;

        _selectedAccount = acc;
        TxtSender.Text = $"{acc.AccountNumber} | Баланс: {acc.Balance:N0} ₽";
        UpdateCommissionInfo();

        LoadStatementForSelectedAccount();
    }

    private void LoadStatementForSelectedAccount()
    {
        if (_selectedAccount == null) return;

        try
        {
            var history = _service.GetStatement(_selectedAccount.Id);

            var displayData = history.Select(t => new
            {
                Date = t.Date.ToString("dd.MM.yyyy HH:mm"),
                Type = TranslateTransactionType(t.Type),
                SenderAccount = t.SenderAccountId == _selectedAccount.Id ? "Вы" : _db.Accounts.Find(t.SenderAccountId)?.AccountNumber ?? "Неизвестно",
                ReceiverAccount = t.ReceiverAccountId == _selectedAccount.Id ? "Вы" : _db.Accounts.Find(t.ReceiverAccountId)?.AccountNumber ?? "Неизвестно",
                Amount = t.Amount.ToString("N2") + " ₽",
                Commission = t.Commission.ToString("N2") + " ₽",
                TotalAmount = t.SenderAccountId == _selectedAccount.Id
                    ? (t.Amount + t.Commission).ToString("N2") + " ₽"
                    : t.Amount.ToString("N2") + " ₽"
            }).ToList();

            GridStatement.ItemsSource = null;
            GridStatement.ItemsSource = displayData;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки истории: {ex.Message}");
        }
    }

    private void Amount_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^[0-9.,]*$");
    }

    private void TxtAmount_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (LblCommissionInfo == null) return;
        UpdateCommissionInfo();
    }

    private void UpdateCommissionInfo()
    {
        if (decimal.TryParse(TxtAmount.Text, out decimal amount))
        {
            var commission = _service.CalculateCommission(amount);
            var total = amount + commission;
            LblCommissionInfo.Text = $"Комиссия: {commission:N2} ₽ | Списано всего: {total:N2} ₽";
        }
        else
        {
            LblCommissionInfo.Text = "Комиссия: 0.5% до 10 000 ₽, 1% свыше";
        }
    }

    private void TxtReceiver_GotFocus(object sender, RoutedEventArgs e)
    {
        if (TxtReceiver.Text == "Введите ID получателя" && TxtReceiver.Foreground == Brushes.Gray)
        {
            TxtReceiver.Text = "";
            TxtReceiver.Foreground = Brushes.Black;
        }
    }

    private void TxtReceiver_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtReceiver.Text))
        {
            TxtReceiver.Text = "Введите ID получателя";
            TxtReceiver.Foreground = Brushes.Gray;
        }
    }

    private void ShowTransfer_Click(object s, RoutedEventArgs e) => SwitchPanel(TransferPanel);

    private void ShowStatement_Click(object s, RoutedEventArgs e)
    {
        if (_selectedAccount == null)
        {
            MessageBox.Show("Сначала выберите счёт в меню слева.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SwitchPanel(StatementPanel);
    }

    private string TranslateTransactionType(string type)
    {
        return type?.ToLower() switch
        {
            "transfer" => "Перевод",
            "deposit" => "Пополнение",
            "withdrawal" => "Снятие",
            "payment" => "Оплата",
            _ => type ?? "Операция"
        };
    }

    private void ShowLoanCalc_Click(object s, RoutedEventArgs e) => SwitchPanel(LoanPanel);

    private void SwitchPanel(StackPanel panel)
    {
        TransferPanel.Visibility = Visibility.Collapsed;
        StatementPanel.Visibility = Visibility.Collapsed;
        LoanPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
    }

    private void DoTransfer_Click(object s, RoutedEventArgs e)
    {
        if (_selectedAccount == null)
        {
            MessageBox.Show("Выберите счёт отправителя!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (TxtReceiver.Text == "Введите ID получателя" || TxtReceiver.Foreground == Brushes.Gray)
        {
            LblResult.Text = "❌ Введите ID получателя";
            LblResult.Foreground = Brushes.Crimson;
            return;
        }

        if (!int.TryParse(TxtReceiver.Text, out int receiverId))
        {
            LblResult.Text = " ID получателя должен быть числом";
            LblResult.Foreground = Brushes.Crimson;
            return;
        }

        if (!decimal.TryParse(TxtAmount.Text, out decimal amount) || amount <= 0)
        {
            LblResult.Text = "❌ Введите корректную сумму";
            LblResult.Foreground = Brushes.Crimson;
            return;
        }

        try
        {
            bool success = _service.TransferMoney(_selectedAccount.Id, receiverId, amount, out string msg);

            if (success)
            {
                LblResult.Text = $"✅ {msg}";
                LblResult.Foreground = Brushes.ForestGreen;

                var updatedAccount = _db.Accounts.Find(_selectedAccount.Id);
                if (updatedAccount != null)
                {
                    _selectedAccount.Balance = updatedAccount.Balance;
                }

                TxtSender.Text = $"{_selectedAccount.AccountNumber} | Баланс: {_selectedAccount.Balance:N0} ₽";

                LoadAccounts();
                AccountsList.SelectedItem = _selectedAccount;

                LoadStatementForSelectedAccount();

                TxtReceiver.Text = "Введите ID получателя";
                TxtReceiver.Foreground = Brushes.Gray;
                TxtAmount.Text = "0";
                UpdateCommissionInfo();
            }
            else
            {
                LblResult.Text = $"❌ {msg}";
                LblResult.Foreground = Brushes.Crimson;
            }
        }
        catch (Exception ex)
        {
            LblResult.Text = $"❌ Ошибка перевода: {ex.Message}";
            LblResult.Foreground = Brushes.Crimson;
        }
    }

    private void CalcLoan_Click(object s, RoutedEventArgs e)
    {
        if (decimal.TryParse(LoanAmount.Text, out decimal sum) &&
            double.TryParse(LoanRate.Text, out double rate) &&
            int.TryParse(LoanTerm.Text, out int term) && sum > 0 && term > 0)
        {
            var payment = _service.CalculateMonthlyPayment(sum, rate, term);
            var totalPayment = payment * term;
            var overpayment = totalPayment - sum;

            LoanResult.Text = $"💰 {payment:N2} ₽ / мес";
            LoanDetails.Text = $"Всего к выплате: {totalPayment:N2} ₽ | Переплата: {overpayment:N2} ₽";
        }
        else
        {
            LoanResult.Text = "❌ Проверьте введённые данные";
            LoanDetails.Text = "";
        }
    }
}