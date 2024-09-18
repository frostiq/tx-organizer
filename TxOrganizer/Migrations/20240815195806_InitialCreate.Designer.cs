﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace TxOrganizer.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20240815195806_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.20");

            modelBuilder.Entity("CoinGeckoId", b =>
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
                            Symbol = "btc",
                            CoinId = "bitcoin"
                        },
                        new
                        {
                            Symbol = "eth",
                            CoinId = "ethereum"
                        });
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
                        .HasColumnType("DATETIME");

                    b.Property<double>("Fee")
                        .HasColumnType("REAL");

                    b.Property<string>("FeeCurrency")
                        .HasColumnType("TEXT");

                    b.Property<string>("Location")
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
