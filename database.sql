IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(255) NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] nvarchar(50) NOT NULL,
    [FullName] nvarchar(100) NOT NULL,
    [Email] nvarchar(100) NOT NULL,
    [PasswordHash] nvarchar(255) NOT NULL,
    [Phone] nvarchar(20) NULL,
    [Address] nvarchar(255) NULL,
    [Avatar] nvarchar(max) NULL,
    [Role] nvarchar(20) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE TABLE [Vouchers] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(50) NOT NULL,
    [DiscountAmount] decimal(18,2) NULL,
    [MinOrderValue] decimal(18,2) NOT NULL,
    [ExpiryDate] datetime2 NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Vouchers] PRIMARY KEY ([Id])
);

CREATE TABLE [Products] (
    [Id] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Price] decimal(18,2) NOT NULL,
    [ImageUrl] nvarchar(max) NULL,
    [CategoryId] int NULL,
    [IsAvailable] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id])
);

CREATE TABLE [Orders] (
    [Id] nvarchar(50) NOT NULL,
    [UserId] nvarchar(50) NOT NULL,
    [OrderDate] datetime2 NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [DiscountAmount] decimal(18,2) NOT NULL,
    [FinalAmount] decimal(18,2) NOT NULL,
    [PaymentMethod] nvarchar(50) NULL,
    [Status] nvarchar(50) NOT NULL,
    [ShippingAddress] nvarchar(255) NOT NULL,
    [VoucherId] int NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Orders_Vouchers_VoucherId] FOREIGN KEY ([VoucherId]) REFERENCES [Vouchers] ([Id])
);

CREATE TABLE [CartItems] (
    [UserId] nvarchar(50) NOT NULL,
    [ProductId] nvarchar(50) NOT NULL,
    [Quantity] int NOT NULL,
    CONSTRAINT [PK_CartItems] PRIMARY KEY ([UserId], [ProductId]),
    CONSTRAINT [FK_CartItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CartItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Reviews] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(50) NOT NULL,
    [ProductId] nvarchar(50) NOT NULL,
    [Rating] int NOT NULL,
    [Comment] nvarchar(max) NULL,
    [IsHidden] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reviews_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Reviews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [OrderDetails] (
    [OrderId] nvarchar(50) NOT NULL,
    [ProductId] nvarchar(50) NOT NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_OrderDetails] PRIMARY KEY ([OrderId], [ProductId]),
    CONSTRAINT [FK_OrderDetails_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderDetails_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_CartItems_ProductId] ON [CartItems] ([ProductId]);

CREATE INDEX [IX_OrderDetails_ProductId] ON [OrderDetails] ([ProductId]);

CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);

CREATE INDEX [IX_Orders_VoucherId] ON [Orders] ([VoucherId]);

CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);

CREATE INDEX [IX_Reviews_ProductId] ON [Reviews] ([ProductId]);

CREATE INDEX [IX_Reviews_UserId] ON [Reviews] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260314074037_InitialCreate', N'9.0.0');

ALTER TABLE [Categories] ADD [ImageUrl] nvarchar(max) NULL;

ALTER TABLE [Categories] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260314140133_UpdateCategory2', N'9.0.0');

ALTER TABLE [Vouchers] ADD [DiscountPercent] decimal(18,2) NULL;

ALTER TABLE [Vouchers] ADD [Name] nvarchar(255) NOT NULL DEFAULT N'';

ALTER TABLE [Vouchers] ADD [StartDate] datetime2 NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260314161948_AddVoucherFields', N'9.0.0');

ALTER TABLE [Vouchers] ADD [MaxUses] int NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260315152646_AddVoucherMaxUses', N'9.0.0');

ALTER TABLE [Users] ADD [DateOfBirth] datetime2 NULL;

ALTER TABLE [Users] ADD [Gender] nvarchar(20) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260317190835_AddUserGenderDob', N'9.0.0');

DROP TABLE [CartItems];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260321160007_RemoveCartItemsTable', N'9.0.0');

UPDATE Users SET Role = '1' WHERE Role = 'Admin'

UPDATE Users SET Role = '2' WHERE Role = 'Staff'

UPDATE Users SET Role = '3' WHERE Role = 'Customer'

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'Role');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Users] ALTER COLUMN [Role] int NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260321163921_ChangeRoleToEnum', N'9.0.0');

CREATE TABLE [Toppings] (
    [Id] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [ImageUrl] nvarchar(max) NULL,
    [IsAvailable] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Toppings] PRIMARY KEY ([Id])
);

CREATE TABLE [ProductToppings] (
    [ProductId] nvarchar(50) NOT NULL,
    [ToppingId] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_ProductToppings] PRIMARY KEY ([ProductId], [ToppingId]),
    CONSTRAINT [FK_ProductToppings_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProductToppings_Toppings_ToppingId] FOREIGN KEY ([ToppingId]) REFERENCES [Toppings] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_ProductToppings_ToppingId] ON [ProductToppings] ([ToppingId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260322163148_AddToppingsModule', N'9.0.0');

ALTER TABLE [Toppings] ADD [UpdatedAt] datetime2 NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260322164959_UpdateToppingModel', N'9.0.0');

ALTER TABLE [Products] ADD [ImageEmbedding] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260331185421_AddImageEmbedding', N'9.0.0');

CREATE TABLE [ContactRequests] (
    [Id] int NOT NULL IDENTITY,
    [FullName] nvarchar(100) NOT NULL,
    [OrderCode] nvarchar(100) NULL,
    [IssueType] nvarchar(50) NOT NULL,
    [Message] nvarchar(1000) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UserId] nvarchar(50) NULL,
    CONSTRAINT [PK_ContactRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ContactRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);

CREATE INDEX [IX_ContactRequests_UserId] ON [ContactRequests] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260408165044_AddContactRequests', N'9.0.0');

ALTER TABLE [ContactRequests] ADD [Address] nvarchar(255) NULL;

ALTER TABLE [ContactRequests] ADD [Phone] nvarchar(20) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260408172554_AddPhoneAndAddressToContactRequest', N'9.0.0');

CREATE TABLE [ContactMessages] (
    [Id] int NOT NULL IDENTITY,
    [ContactRequestId] int NOT NULL,
    [Sender] nvarchar(20) NOT NULL,
    [Content] nvarchar(2000) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ContactMessages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ContactMessages_ContactRequests_ContactRequestId] FOREIGN KEY ([ContactRequestId]) REFERENCES [ContactRequests] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_ContactMessages_ContactRequestId] ON [ContactMessages] ([ContactRequestId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260408191601_AddContactMessages', N'9.0.0');

ALTER TABLE [Reviews] ADD [AdminReply] nvarchar(1000) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260408193105_AddAdminReplyToReview', N'9.0.0');

COMMIT;
GO

