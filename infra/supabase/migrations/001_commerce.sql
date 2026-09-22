-- OMSI Map Studio Public Alpha commerce foundation.
create extension if not exists pgcrypto;

create table if not exists public.profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  email text,
  display_name text,
  role text not null default 'subscriber'
    check (role in ('subscriber','admin')),
  created_at timestamptz not null default now()
);

create table if not exists public.customers (
  user_id uuid primary key references auth.users(id) on delete cascade,
  stripe_customer_id text unique not null,
  created_at timestamptz not null default now()
);

create table if not exists public.subscriptions (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  stripe_subscription_id text unique not null,
  stripe_customer_id text,
  stripe_price_id text,
  status text not null,
  current_period_end timestamptz not null,
  cancel_at_period_end boolean not null default false,
  updated_at timestamptz not null default now()
);

create table if not exists public.licenses (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  serial text unique not null,
  status text not null default 'active'
    check (status in ('active','revoked')),
  max_devices integer not null default 2
    check (max_devices between 1 and 20),
  created_at timestamptz not null default now()
);

create table if not exists public.devices (
  id uuid primary key default gen_random_uuid(),
  license_id uuid not null references public.licenses(id) on delete cascade,
  device_id text not null,
  device_name text not null default 'Windows PC',
  activated_at timestamptz not null default now(),
  last_seen_at timestamptz not null default now(),
  revoked_at timestamptz,
  unique (license_id, device_id)
);

create table if not exists public.entitlements (
  user_id uuid not null references auth.users(id) on delete cascade,
  entitlement_key text not null,
  enabled boolean not null default false,
  source text not null default 'stripe',
  expires_at timestamptz,
  updated_at timestamptz not null default now(),
  primary key (user_id, entitlement_key)
);

create table if not exists public.releases (
  id uuid primary key default gen_random_uuid(),
  version text not null,
  channel text not null check (channel in ('alpha','stable')),
  notes text not null default '',
  minimum_version text,
  mandatory boolean not null default false,
  published boolean not null default false,
  published_at timestamptz not null default now(),
  download_url text not null,
  sha256 text,
  created_by uuid references auth.users(id),
  unique (version, channel)
);

create table if not exists public.audit_logs (
  id bigint generated always as identity primary key,
  actor_user_id uuid references auth.users(id),
  event_type text not null,
  target_type text,
  target_id text,
  details jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now()
);

create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer set search_path = public
as $$
begin
  insert into public.profiles(id,email,display_name)
  values (
    new.id,
    new.email,
    coalesce(new.raw_user_meta_data->>'display_name','')
  )
  on conflict (id) do nothing;

  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;

create trigger on_auth_user_created
after insert on auth.users
for each row execute procedure public.handle_new_user();

create or replace function public.is_admin()
returns boolean
language sql
stable
security definer set search_path = public
as $$
  select exists(
    select 1
    from public.profiles
    where id = auth.uid()
      and role = 'admin'
  );
$$;

alter table public.profiles enable row level security;
alter table public.customers enable row level security;
alter table public.subscriptions enable row level security;
alter table public.licenses enable row level security;
alter table public.devices enable row level security;
alter table public.entitlements enable row level security;
alter table public.releases enable row level security;
alter table public.audit_logs enable row level security;

create policy "profile own or admin"
on public.profiles for select
using (id = auth.uid() or public.is_admin());

create policy "subscription own or admin"
on public.subscriptions for select
using (user_id = auth.uid() or public.is_admin());

create policy "license own or admin"
on public.licenses for select
using (user_id = auth.uid() or public.is_admin());

create policy "device own or admin"
on public.devices for select
using (
  exists (
    select 1
    from public.licenses license
    where license.id = license_id
      and (
        license.user_id = auth.uid()
        or public.is_admin()
      )
  )
);

create policy "entitlement own or admin"
on public.entitlements for select
using (user_id = auth.uid() or public.is_admin());

create policy "release read published"
on public.releases for select
using (published = true or public.is_admin());

create policy "release admin insert"
on public.releases for insert
with check (public.is_admin());

create policy "release admin update"
on public.releases for update
using (public.is_admin())
with check (public.is_admin());

create policy "audit admin read"
on public.audit_logs for select
using (public.is_admin());
