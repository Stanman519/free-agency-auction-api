-- Many Auth0 subs -> one owner (Google login one year, email/password the next).
-- Run once on Neon (try a branch first), BEFORE deploying the matching API build.
create table if not exists owneridentity (
    id         serial primary key,
    ownerid    integer not null references owner(ownerid) on delete cascade,
    authid     text    not null,
    createdat  timestamp not null default (now() at time zone 'utc')
);
create unique index if not exists ix_owneridentity_authid on owneridentity (authid);

-- Backfill: every existing owner's current authid becomes an identity.
insert into owneridentity (ownerid, authid)
select ownerid, authid from owner
where authid is not null and authid <> ''
on conflict (authid) do nothing;
