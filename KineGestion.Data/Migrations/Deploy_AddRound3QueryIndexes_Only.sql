BEGIN TRANSACTION;
GO

DROP INDEX [IX_Treatments_PatientId] ON [Treatments];
GO

CREATE INDEX [IX_Treatments_PatientId_FechaInicio] ON [Treatments] ([PatientId], [FechaInicio]);
GO

CREATE INDEX [IX_Sessions_PaymentStatus_FechaHora] ON [Sessions] ([PaymentStatus], [FechaHora]);
GO

CREATE INDEX [IX_Professionals_IsActivo_Apellido_Nombre] ON [Professionals] ([IsActivo], [Apellido], [Nombre]);
GO

CREATE INDEX [IX_Patients_IsActivo_Apellido_Nombre] ON [Patients] ([IsActivo], [Apellido], [Nombre]);
GO

CREATE INDEX [IX_AspNetUsers_Email] ON [AspNetUsers] ([Email]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260506234906_AddRound3QueryIndexes', N'8.0.0');
GO

COMMIT;
GO

