using System;
using System.Collections.Generic;
using System.Linq;
using InternetBankApp.Models;
using Microsoft.EntityFrameworkCore;

namespace InternetBankApp.Services;

public class BankService
{
    private readonly BankDbContext _context;

    public BankService(BankDbContext context)
    {
        _context = context;
    }

    public bool TransferMoney(int senderId, int receiverId, decimal amount, out string message)
    {
        message = "";
        var sender = _context.Accounts.Find(senderId);
        var receiver = _context.Accounts.Find(receiverId);

        if (sender == null || receiver == null)
        {
            message = "Счет не найден.";
            return false;
        }

        var commission = CalculateCommission(amount);
        var totalDebit = amount + commission;

        if (sender.Balance < totalDebit)
        {
            message = $"Недостаточно средств. Нужно: {totalDebit:F2}, есть: {sender.Balance:F2}";
            return false;
        }

        sender.Balance -= totalDebit;
        receiver.Balance += amount;

        var transaction = new Transaction
        {
            SenderAccountId = senderId,
            ReceiverAccountId = receiverId,
            Amount = amount,
            Commission = commission,
            Date = DateTime.UtcNow,
            Type = "Transfer"
        };

        _context.Transactions.Add(transaction);
        _context.SaveChanges();

        message = $"Успешно переведено {amount:N2} руб. Комиссия: {commission:N2} руб.";
        return true;
    }

    public decimal CalculateCommission(decimal amount)
    {
        // Формула: до 10 000 ₽ — 0.5%, свыше — 1%
        return amount > 10000 ? amount * 0.01m : amount * 0.005m;
    }

    public List<Transaction> GetStatement(int accountId)
    {
        return _context.Transactions
            .Where(t => t.SenderAccountId == accountId || t.ReceiverAccountId == accountId)
            .OrderByDescending(t => t.Date)
            .ToList();
    }

    public decimal CalculateMonthlyPayment(decimal amount, double ratePercent, int months)
    {
        if (ratePercent == 0) return amount / months;

        double monthlyRate = ratePercent / 12 / 100;
        double k = (monthlyRate * Math.Pow(1 + monthlyRate, months)) / (Math.Pow(1 + monthlyRate, months) - 1);
        return (decimal)(amount * (decimal)k);
    }
}