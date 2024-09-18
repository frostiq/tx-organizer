﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace TxOrganizer.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.20");

            modelBuilder.Entity("TxOrganizer.DTO.CoinGeckoId", b =>
                {
                    b.Property<string>("Symbol")
                        .HasColumnType("TEXT");

                    b.Property<string>("CoinId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Symbol");

                    b.ToTable("CoinGeckoIds");

                    b.HasData(
                        new
                        {
                            Symbol = "BTC",
                            CoinId = "bitcoin"
                        },
                        new
                        {
                            Symbol = "ETH",
                            CoinId = "ethereum"
                        });
                });

            modelBuilder.Entity("TxOrganizer.DTO.Setting", b =>
                {
                    b.Property<string>("Type")
                        .HasColumnType("TEXT");

                    b.Property<string>("Key")
                        .HasColumnType("TEXT");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Type", "Key");

                    b.ToTable("Settings");
                });

            modelBuilder.Entity("TxOrganizer.DTO.Transaction", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<double>("BuyAmount")
                        .HasColumnType("REAL");

                    b.Property<string>("BuyCurrency")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Comment")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("Date")
                        .HasColumnType("TEXT");

                    b.Property<double>("Fee")
                        .HasColumnType("REAL");

                    b.Property<string>("FeeCurrency")
                        .HasColumnType("TEXT");

                    b.Property<string>("Location")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<double>("SellAmount")
                        .HasColumnType("REAL");

                    b.Property<string>("SellCurrency")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("TxHash")
                        .HasColumnType("TEXT");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<double>("USDEquivalent")
                        .HasColumnType("REAL");

                    b.HasKey("Id");

                    b.ToTable("Transactions");
                });
#pragma warning restore 612, 618
        }
    }
}
