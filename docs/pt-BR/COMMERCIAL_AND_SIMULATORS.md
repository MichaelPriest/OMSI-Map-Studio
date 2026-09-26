# Arquitetura comercial e compatibilidade multi-simulador

Este documento registra a separação entre edição, cobrança, licença e suporte a simuladores.

## 1. Estado atual

Durante o desenvolvimento e validação Alpha, o Map Studio opera em `DevelopmentPreview`:

- cobrança não é aplicada;
- todas as funcionalidades continuam disponíveis;
- nenhuma licença falsa é gerada;
- nenhum servidor de licença é simulado;
- o PR de desenvolvimento continua podendo ser testado sem conta comercial.

Quando o produto entrar em distribuição comercial, `EnforcementEnabled` poderá ser ativado e o mesmo feature gate passará a exigir entitlements reais.

## 2. Stripe: cobrança, não autoridade do editor

Stripe será o provedor inicial de billing/assinaturas.

O aplicativo desktop NÃO deve:

- conter chave secreta Stripe;
- criar assinatura diretamente com chave privilegiada;
- validar webhook;
- decidir sozinho se uma assinatura está ativa;
- confiar apenas em estado salvo localmente.

Fluxo previsto:

1. Desktop autentica o usuário no serviço comercial do Map Studio.
2. Desktop pede ao backend uma URL de Checkout.
3. Backend cria uma Stripe Checkout Session em modo de assinatura.
4. Usuário conclui o pagamento na página hospedada.
5. Stripe envia webhooks ao backend.
6. Backend converte estado de assinatura em entitlements Map Studio.
7. Desktop consulta um estado de entitlement assinado/validável.
8. Customer Portal é criado pelo backend quando o usuário quiser gerenciar a assinatura.

A implementação do backend de licença/entitlement será definida depois.

## 3. Entitlements

O Core já possui chaves independentes de preço e de produto Stripe:

- `editor.core`;
- `creation.building-studio`;
- `creation.procedural-roads`;
- `creation.ai-assistance`;
- `simulator.omsi2`;
- `simulator.proton-bus`;
- `simulator.lotus`.

Isso permite decidir mais tarde se teremos:

- um único plano com tudo;
- planos por nível;
- add-ons por simulador;
- add-on de IA;
- licenças perpétuas para versões específicas;
- assinatura mensal/anual;
- combinações dessas opções.

Nenhuma dessas decisões comerciais está codificada como preço hoje.

## 4. Licença e cobrança são camadas diferentes

Stripe informa o estado de cobrança. O backend comercial do Map Studio será responsável por transformar esse estado em acesso ao produto.

A camada de licença/entitlement futura deve considerar:

- conta do usuário;
- assinatura ativa;
- período de graça;
- cancelamento ao fim do ciclo;
- atraso/falha de pagamento;
- dispositivos/sessões, se adotados;
- cache offline com validade limitada, se adotado;
- revogação;
- assinatura criptográfica do entitlement;
- migração entre planos.

Essas regras não devem entrar nos parsers ou writers de formatos dos simuladores.

## 5. Arquitetura multi-simulador

O projeto passa a possuir uma fronteira formal de adapters em `MapStudio.Core.Simulators`.

Hoje:

- OMSI 2 possui adapter registrado;
- Proton Bus é um ID conhecido, mas NÃO é anunciado como suportado;
- LOTUS é um ID conhecido, mas NÃO é anunciado como suportado;
- futuros simuladores poderão ser adicionados por adapter real.

Um adapter declara capacidades como:

- descoberta de mapas;
- edição de mapas;
- terreno;
- vias;
- objetos;
- biblioteca;
- tráfego;
- transporte público;
- sinais/ferrovia;
- criação procedural;
- Building Studio;
- validação.

Uma plataforma só deve ser marcada como suportada quando seu adapter realmente implementar os fluxos necessários.

## 6. Separação de responsabilidades

A direção alvo é:

```text
Map Studio WinUI
    |
    +-- Core comercial / feature gates
    |
    +-- Core de criação procedural
    |
    +-- Building Studio / IA
    |
    +-- Simulator Registry
           |
           +-- OMSI 2 adapter
           +-- Proton Bus adapter (futuro)
           +-- LOTUS adapter (futuro)
           +-- outros adapters
```

Cada adapter traduz o modelo do Map Studio para os formatos reais do simulador correspondente.

## 7. Regra para IA

IA continua sendo agnóstica de provedor.

A assinatura do Map Studio e o provedor de IA são conceitos diferentes:

- o cliente pode assinar o Map Studio;
- o plano pode liberar o entitlement de IA;
- o cliente pode conectar o provedor de IA que preferir;
- chaves do provedor de IA não são chaves Stripe;
- billing não deve determinar qual fornecedor de IA o usuário é obrigado a usar.

## 8. Próximas etapas comerciais

Depois da arquitetura do editor estabilizar:

1. definir produtos/planos;
2. criar backend comercial;
3. configurar Stripe em sandbox;
4. criar Checkout de assinatura;
5. criar Customer Portal;
6. receber e validar webhooks;
7. persistir contas/assinaturas/entitlements;
8. definir política offline;
9. assinar respostas de entitlement;
10. ativar `EnforcementEnabled` somente após testes completos.

Até essa etapa, o modo de desenvolvimento continua explicitamente desbloqueado.
