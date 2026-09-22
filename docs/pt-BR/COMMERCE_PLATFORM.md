# Plataforma comercial do OMSI Map Studio

## Objetivo

A plataforma do Public Alpha é separada em quatro áreas:

1. site público de divulgação e compra;
2. área autenticada do assinante;
3. painel administrativo;
4. APIs de licença e atualização consumidas pelo aplicativo WinUI.

## Stack inicial

- Next.js para site, portais e endpoints;
- Supabase Auth + PostgreSQL para contas, assinaturas, licenças, dispositivos, entitlements e releases;
- Stripe Checkout + webhooks para cobrança;
- Ed25519 para emitir tokens de entitlement verificáveis offline pelo aplicativo.

Segredos Stripe, Service Role do Supabase e chave privada de assinatura ficam somente no servidor.

## Licença

A compra ativa o entitlement `editor.core` e cria um serial no formato:

`OMS-MS-A5-XXXX-XXXX-XXXX`

O endpoint de ativação registra o computador, respeita o limite da licença e devolve um token assinado. O aplicativo deve conter somente a chave pública Ed25519 para validar esse token offline.

A configuração inicial usa até 2 computadores por licença.

## Atualizações

O painel Admin publica um manifesto de versão por canal `alpha` ou `stable`.

O aplicativo consulta:

`GET /api/updates/latest?channel=alpha`

O manifesto informa versão, notas, hash, versão mínima e se a atualização é obrigatória.

O download passa por:

`GET /api/download/latest?channel=alpha`

Esse endpoint exige o entitlement `editor.core`.

## Estado da primeira implementação

Já existe na branch comercial:

- home pública;
- página de assinatura;
- cadastro/login;
- Stripe Checkout;
- webhook de assinatura;
- criação automática do serial;
- entitlement `editor.core`;
- ativação por dispositivo;
- token offline Ed25519;
- área do assinante;
- painel Admin;
- cadastro de releases;
- manifesto de atualização;
- download protegido por entitlement;
- schema PostgreSQL + RLS.

Ainda falta para produção:

- criar um projeto Supabase exclusivo do Map Studio;
- configurar o produto/preço no Stripe;
- registrar o webhook Stripe;
- configurar variáveis de ambiente;
- gerar o par de chaves Ed25519;
- implementar Customer Portal;
- permitir ao assinante revogar dispositivos;
- integrar licença e update checker no WinUI;
- publicar o site em domínio oficial;
- revisar termos, privacidade e política do Public Alpha.
