-- Extensão para gerar UUIDs
-- postgres
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Schemas
CREATE SCHEMA IF NOT EXISTS contacorrente;
CREATE SCHEMA IF NOT EXISTS transferencia;
CREATE SCHEMA IF NOT EXISTS tarifas;

-- ================================
-- SCHEMA: contacorrente
-- ================================
CREATE TABLE IF NOT EXISTS contacorrente.contacorrente (
  idcontacorrente   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  numero            varchar(20)  NOT NULL UNIQUE,   -- preserva zeros à esquerda
  nome              varchar(100) NOT NULL,
  ativo             boolean      NOT NULL DEFAULT false,
  cpf               varchar(11)  NOT NULL UNIQUE,
  senha             text         NOT NULL,
  salt              text         NOT NULL
);

CREATE TABLE IF NOT EXISTS contacorrente.movimento (
  idmovimento       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  idcontacorrente   uuid NOT NULL
                    REFERENCES contacorrente.contacorrente(idcontacorrente),
  datamovimento     timestamptz NOT NULL,
  tipomovimento     char(1)     NOT NULL CHECK (tipomovimento IN ('C','D')),
  valor             numeric(14,2) NOT NULL CHECK (valor > 0)
);

CREATE TABLE IF NOT EXISTS contacorrente.idempotencia (
  chave_idempotencia varchar(100) PRIMARY KEY,
  requisicao         jsonb,
  resultado          jsonb,
  created_at         timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_movimento_conta_data
  ON contacorrente.movimento (idcontacorrente, datamovimento);

-- ================================
-- SCHEMA: transferencia
-- ================================
CREATE TABLE IF NOT EXISTS transferencia.transferencia (
  idtransferencia          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  idcontacorrente_origem   uuid NOT NULL
                           REFERENCES contacorrente.contacorrente(idcontacorrente),
  idcontacorrente_destino  uuid NOT NULL
                           REFERENCES contacorrente.contacorrente(idcontacorrente),
  datamovimento            timestamptz NOT NULL,
  valor                    numeric(14,2) NOT NULL CHECK (valor > 0)
);

CREATE TABLE IF NOT EXISTS transferencia.idempotencia (
  chave_idempotencia varchar(100) PRIMARY KEY,
  requisicao         jsonb,
  resultado          jsonb,
  created_at         timestamptz NOT NULL DEFAULT now()
);

-- ================================
-- SCHEMA: tarifas
-- ================================
CREATE TABLE IF NOT EXISTS tarifas.tarifas (
  idtarifa          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  idcontacorrente   uuid NOT NULL
                    REFERENCES contacorrente.contacorrente(idcontacorrente),
  datamovimento     timestamptz NOT NULL,
  valor             numeric(14,2) NOT NULL CHECK (valor > 0)
);