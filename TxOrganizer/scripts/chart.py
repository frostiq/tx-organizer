import mplcursors as mplcursors
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.dates as mdates

# Read the CSV files
balances_df = pd.read_csv('../total_quantity_history_balances.csv')
taxlots_df = pd.read_csv('../total_quantity_history_taxlots.csv')

# Ensure the time column is in datetime format
balances_df['Time'] = pd.to_datetime(balances_df['Time'])
taxlots_df['Time'] = pd.to_datetime(taxlots_df['Time'])
 
# Plot the time series data
# plt.figure(figsize=(10, 6))
# plt.plot(balances_df['Time'], balances_df['Quantity'], label='by balances')
# plt.plot(taxlots_df['Time'], taxlots_df['Quantity'], label='by taxlots')
# 
# plt.xlabel('Time')
# plt.ylabel('Quantity')
# plt.title('Time Series Chart')
# plt.legend()
# plt.show()

# Resample the data to a monthly frequency
balances_df_monthly = balances_df.resample('W', on='Time').max()
taxlots_df_monthly = taxlots_df.resample('W', on='Time').max()

# Calculate the difference between the 'Quantity' columns of the two resampled DataFrames
balances_df_monthly['Quantity_diff'] = (balances_df_monthly['Quantity'] - taxlots_df_monthly['Quantity']).diff()

# Plot the difference
plt.figure(figsize=(10, 6))
bars = plt.bar(balances_df_monthly.index, balances_df_monthly['Quantity_diff'], label='Difference by week', width=5.0)

plt.xlabel('Time')
plt.ylabel('Quantity Difference')
plt.title('Time Series Chart of Quantity Difference')
plt.legend()

# Add tooltips to the chart
cursor = mplcursors.cursor(bars, hover=True)
cursor.connect("add", lambda sel: sel.annotation.set_text('Date: {}\nQuantity Difference: {}'.format(mdates.num2date(sel.target[0]).strftime('%Y-%m-%d'), sel.target[1])))


plt.show()