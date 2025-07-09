--author luis diaz
--proyecto spgg ti

CREATE TABLE dbo.Domicilios (
    id_domicilio       INT            IDENTITY(1,1) PRIMARY KEY,
    titular            NVARCHAR(120)  NOT NULL,
    latitude           NVARCHAR(25)   NOT NULL,
    longitude          NVARCHAR(25)   NOT NULL,
    direccion          NVARCHAR(250)  NOT NULL,
    fecha_construccion DATE           NULL
);

CREATE TABLE dbo.Prediales (
    id_predial       INT            IDENTITY(1,1) PRIMARY KEY,
    id_domicilio     INT            NOT NULL REFERENCES dbo.Domicilios(id_domicilio),
    monto            DECIMAL(10,2)  NOT NULL,
    fecha_expedida   DATE           NOT NULL,
    fecha_limite     DATE           NOT NULL,
    fecha_pago       DATE           NULL,
    pago_a_tiempo    BIT            NOT NULL DEFAULT 0,
    pagado           BIT            NOT NULL DEFAULT 0
);

CREATE TABLE dbo.TiposAuto (
    id_tipo_auto SMALLINT       IDENTITY(1,1) PRIMARY KEY,
    tipo         NVARCHAR(100)  NOT NULL UNIQUE
);

CREATE TABLE dbo.PlacasAutos (
    id_placa     INT            IDENTITY(1,1) PRIMARY KEY,
    titular      NVARCHAR(120)  NOT NULL,
    id_tipo_auto SMALLINT       NOT NULL REFERENCES dbo.TiposAuto(id_tipo_auto),
    placa        VARCHAR(10)    NOT NULL UNIQUE
);


CREATE TABLE dbo.TiposMulta (
    id_tipo_multa SMALLINT       IDENTITY(1,1) PRIMARY KEY,
    titulo        NVARCHAR(120)  NOT NULL UNIQUE,
    monto         DECIMAL(10,2)  NOT NULL
);

CREATE TABLE dbo.Multas (
    id_multa        INT            IDENTITY(1,1) PRIMARY KEY,
    id_placa        INT            NOT NULL REFERENCES dbo.PlacasAutos(id_placa),
    id_tipo_multa   SMALLINT       NOT NULL REFERENCES dbo.TiposMulta(id_tipo_multa),
    monto           DECIMAL(10,2)  NOT NULL,
    latitude        NVARCHAR(25)   NOT NULL,
    longitude       NVARCHAR(25)   NOT NULL,
    direccion       NVARCHAR(250)  NOT NULL,
    detalle         NVARCHAR(500)  NULL,
    fecha_expedida  DATE           NOT NULL,
    fecha_limite    DATE           NOT NULL,
    fecha_pago      DATE           NULL,
    pago_a_tiempo   BIT            NOT NULL DEFAULT 0,
    pagado           BIT            NOT NULL DEFAULT 0
);
