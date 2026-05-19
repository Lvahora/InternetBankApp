using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InternetBankApp.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public List<BankAccount> Accounts { get; set; } = new();
}

public class BankAccount : INotifyPropertyChanged
{
    private decimal _balance;
    private string _accountNumber = "";
    private string _currency = "RUB";

    public int Id { get; set; }
    public int UserId { get; set; }

    public string AccountNumber
    {
        get => _accountNumber;
        set { _accountNumber = value; OnPropertyChanged(); }
    }

    public decimal Balance
    {
        get => _balance;
        set { _balance = value; OnPropertyChanged(); }
    }

    public string Currency
    {
        get => _currency;
        set { _currency = value; OnPropertyChanged(); }
    }

    public User User { get; set; } = null!;
    public List<Transaction> TransactionsSent { get; set; } = new();
    public List<Transaction> TransactionsReceived { get; set; } = new();
    public List<Card> Cards { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class Card
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string CardNumberMasked { get; set; } = "";
    public DateTime ExpiryDate { get; set; }
    public BankAccount Account { get; set; } = null!;
}

public class Transaction
{
    public int Id { get; set; }
    public int SenderAccountId { get; set; }
    public int ReceiverAccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal Commission { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public string Type { get; set; } = "Transfer";
    public BankAccount Sender { get; set; } = null!;
    public BankAccount Receiver { get; set; } = null!;
}

public class Loan
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal InterestRate { get; set; }
    public int TermMonths { get; set; }
    public BankAccount Account { get; set; } = null!;
}

public class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<BankAccount> Accounts { get; set; } = null!;
    public DbSet<Card> Cards { get; set; } = null!;
    public DbSet<Transaction> Transactions { get; set; } = null!;
    public DbSet<Loan> Loans { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Sender)
            .WithMany(a => a.TransactionsSent)
            .HasForeignKey(t => t.SenderAccountId);

        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Receiver)
            .WithMany(a => a.TransactionsReceived)
            .HasForeignKey(t => t.ReceiverAccountId);
    }
}