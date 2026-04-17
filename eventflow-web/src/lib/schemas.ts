/*
  ZOD — SCHEMAS DE VALIDAÇÃO DE FORMULÁRIOS
  ──────────────────────────────────────────
  O que é Zod?
  Uma biblioteca de validação de schema com inferência de tipos TypeScript.
  Define o schema UMA vez → TypeScript infere o tipo automaticamente.

  POR QUÊ Zod e não validação manual?
  - Validação manual: `if (!email.includes('@')) setError(...)` — verbose e fácil de esquecer
  - Zod: declara as regras, gera o tipo, e fornece mensagens de erro estruturadas
  - Integra nativamente com React Hook Form via @hookform/resolvers/zod

  FLUXO com React Hook Form:
  1. Schema Zod define as regras + tipo
  2. zodResolver(schema) conecta ao useForm()
  3. useForm() valida em tempo real
  4. Erros aparecem com register().name → formState.errors.fieldName.message

  CONVENÇÃO: schemas no singular + sufixo Schema (loginSchema, registerSchema)
  O tipo inferido: type LoginFormData = z.infer<typeof loginSchema>
*/

import { z } from 'zod';

// ─────────────────────────────────────────────
// AUTENTICAÇÃO
// ─────────────────────────────────────────────

export const loginSchema = z.object({
  email: z
    .string()
    .min(1, 'Email é obrigatório')
    .email('Formato de email inválido'),

  password: z
    .string()
    .min(1, 'Senha é obrigatória'),
});

export type LoginFormData = z.infer<typeof loginSchema>;

export const registerSchema = z
  .object({
    name: z
      .string()
      .min(2, 'Nome deve ter pelo menos 2 caracteres')
      .max(100, 'Nome muito longo'),

    email: z
      .string()
      .min(1, 'Email é obrigatório')
      .email('Formato de email inválido'),

    password: z
      .string()
      .min(6, 'Senha deve ter pelo menos 6 caracteres')
      .regex(/[A-Z]/, 'Senha deve conter pelo menos uma letra maiúscula')
      .regex(/[0-9]/, 'Senha deve conter pelo menos um número'),

    confirmPassword: z
      .string()
      .min(1, 'Confirmação de senha é obrigatória'),

    role: z.enum(['Attendee', 'Organizer'], {
      required_error: 'Selecione um tipo de conta',
    }),
  })
  /*
    .refine() permite validação cross-field — campos que dependem de outros.
    POR QUÊ não validar no campo diretamente?
    confirmPassword não pode saber o valor de password sem acesso ao objeto inteiro.
  */
  .refine((data) => data.password === data.confirmPassword, {
    message: 'As senhas não coincidem',
    path: ['confirmPassword'],  // Atribui o erro ao campo confirmPassword
  });

export type RegisterFormData = z.infer<typeof registerSchema>;

// ─────────────────────────────────────────────
// EVENTOS
// ─────────────────────────────────────────────

export const createEventSchema = z
  .object({
    title: z
      .string()
      .min(3, 'Título deve ter pelo menos 3 caracteres')
      .max(200, 'Título muito longo'),

    description: z
      .string()
      .min(10, 'Descrição deve ter pelo menos 10 caracteres')
      .max(5000, 'Descrição muito longa'),

    location: z
      .string()
      .min(3, 'Localização é obrigatória')
      .max(300, 'Localização muito longa'),

    startDate: z
      .string()
      .min(1, 'Data de início é obrigatória')
      .refine((val) => !isNaN(Date.parse(val)), 'Data de início inválida'),

    endDate: z
      .string()
      .min(1, 'Data de término é obrigatória')
      .refine((val) => !isNaN(Date.parse(val)), 'Data de término inválida'),

    capacity: z
      .number({ invalid_type_error: 'Capacidade deve ser um número' })
      .int('Capacidade deve ser um número inteiro')
      .min(1, 'Capacidade mínima é 1')
      .max(100_000, 'Capacidade máxima é 100.000'),

    price: z
      .number({ invalid_type_error: 'Preço deve ser um número' })
      .min(0, 'Preço não pode ser negativo')
      .max(99_999, 'Preço máximo excedido'),
  })
  .refine(
    (data) => new Date(data.endDate) > new Date(data.startDate),
    {
      message: 'Data de término deve ser após a data de início',
      path: ['endDate'],
    }
  )
  .refine(
    (data) => new Date(data.startDate) > new Date(),
    {
      message: 'Data de início deve ser no futuro',
      path: ['startDate'],
    }
  );

export type CreateEventFormData = z.infer<typeof createEventSchema>;
