# TxOrganizer

## Description

TxOrganizer is a console app that provides a clear vision of your trading/investing history, enabling you to learn from your experiences and become a better investor or trader.

For many active crypto users, the number of transactions is so large that it becomes challenging to maintain a clear vision of past and current positions and monitor everything in one place.

At the same time, many crypto users file annual tax reports. You can use this information as a source of truth to build a complete picture of your historical activity – eliminating the need to manually keep several spreadsheets up-to-date.

In the current version, only the TokenTax CSV file format is supported. However, more transaction history file formats could be added easily.

## Installation

1. Ensure you have [.NET 5.0](https://dotnet.microsoft.com/download) or later installed.
2. Clone the repository: `git clone git@github.com:frostiq/tx-organizer.git`
3. Navigate to the project directory: `cd tx-organizer`
4. Build the project: `dotnet build`
5. Run the project: `dotnet run`

## Usage
First of all, you need to import your transactions from a CSV file. You can use the `Import transactions` command from the menu to do this.
If you need to update database, you can either do it manually by editing .db file with SQLite browser or update the cvs file and then run `Import transactions` command again.
The latter will erase existing transactions in the local database.

After importing transactions, you can use the following commands:
### Position History
This command will show the history of yor positions/trades.

### Trace Balances
Many of tax prep apps (e.g. TokenTax) are lacking the functionality of tracing the issues in your transaction history.
This command is helpful to identify the blank spots in your transaction history imported from tax software if you suspect that
some transactions are missing or parsed incorrectly by tax preparation software you use.
This command calculates the balances of each of your wallet/exchange for a specified asset.
If you notice that calculated balance is significantly different from the actual balance,
you can investigate the issue further by focusing on the transaction history of the wallet/exchange with incorrect calculated balance.

### Trace tax lots
Tax lots is the virtual entity that is used to track the cost basis of your assets.
Each tax lot represent an amount of currency that you've purchased at a specific price and time.

Each to calculate a cost basis you have when you sell the currency, tax preparation software finds tax lots that are still "unsold"
with the one of the several common methods, such as HIFO, FIFO, LIFO, etc.

If you ever have a problem understanding how your tax preparation software calculates your capital gains and cost basis,
you can use this command to trace how it forms. At the moment, only HIFO method is supported, however other methods can be easily added.

## Contributing
Any contributions to the project are welcome! Here are the steps to contribute:
1. Fork the project on GitHub.
2. Clone the forked repository to your local machine.
3. Create a new branch for your feature or bug fix.
4. Make your changes in the new branch.
5. Commit your changes and push the branch to your forked repository.
6. Open a pull request from the new branch to the original repository's main branch.