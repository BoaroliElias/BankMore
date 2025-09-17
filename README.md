# BankMore
Sistema de crédito baseado em microsserviços, com foco em escalabilidade e segurança.

Microserviço de **conta corrente** e **transferências** com autenticação JWT, CQRS (via MediatR), Dapper e PostgreSQL. Entrega em containers Docker (Compose).

## Arquitetura (visão rápida)

- **ContaCorrente.Api**  
  - Cadastro e login (JWT)  
  - Movimentação ([C]rédito/[D]ébito) com **idempotência**  
  - Consulta de saldo (créditos – débitos)  
- **Transferencia.Api**  
  - Orquestra débito (origem) + crédito (destino) na ContaCorrente.Api  
  - Compensação em caso de falha (crédito de estorno)  
  - Persistência da transferência + **idempotência** própria  
- **PostgreSQL** + **Adminer** (GUI)  
- Comunicação síncrona HTTP entre as APIs (o endpoint da ContaCorrente é resolvido via nome de serviço do Compose: `http://contacorrente-api:8080`)

Tecnologias:
- .NET 8, ASP.NET Core  
- Dapper, MediatR  
- JWT (Microsoft.IdentityModel.Tokens)  
- PostgreSQL 16 (alpine), Adminer  
- Docker Compose

Decisões de Arquitetura
- CQRS com MediatR: separa comandos de consultas e centraliza cross-cutting (validações, logs).
- Dapper: acesso a dados leve e direto (scripts SQL versionados).
- Idempotência: necessária para operações financeiras — replays de requisições não duplicam efeitos.
- Compensação em transferência: falha no crédito do destino → estorno automático na origem.
- Segurança: JWT; dados sensíveis (senhas) armazenados com hash + salt.

## Segurança

- JWT com chave de desenvolvimento (Jwt__Key no docker-compose.yml).
- Para produção, usar secret seguro (Vault/KeyVault) e habilitar validação de emissor/audience conforme necessário.

## Próximos passos (fora do escopo desta entrega)

Testes automatizados (unitários e integração)

Assincronia com Kafka (tópicos de transferências / tarifação)

Cache seletivo (ex.: saldo)

CI/CD e imagens publicadas em registry

### URLs

- Conta Corrente (Swagger): http://localhost:7121/swagger

- Transferência (Swagger): http://localhost:7254/swagger

- Adminer: http://localhost:8080

- Servidor/Host: bankmore-postgres
Usuário: bankmore
Senha: bankmore
Banco: bankmore

### Banco de Dados
- Criado automaticamente na primeira subida via ./db/init.sql.
### Reset do ambiente
- Reset completo (apaga dados do banco):
```bash
docker compose down -v
docker compose up -d --build
```

## Como executar

### Pré-requisitos
- Docker Desktop (ou Docker Engine + Compose)
- Git

### Subir a stack

```bash
git clone https://github.com/BoaroliElias/BankMore.git
cd BankMore/src
docker compose up -d --build
```


### Roadmap (Próximos Passos)
- Testes automatizados
  - Unitários (handlers/validações)
  - Integração (testes de API com banco efêmero)

- Assincronia com Kafka (OPCIONAL do desafio)
  - Produzir evento de “transferência realizada”
  - Serviço de Tarifação consumindo o evento e debitando tarifa

- Cache seletivo (ex.: saldo por janela curta)
- CI/CD (build & push de imagens para registry, pipelines)
- Observabilidade: métricas e tracing (OpenTelemetry)








