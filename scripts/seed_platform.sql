-- Seed data for platform: roles, permissions, users (vendor-related data not included)
-- Run in psql against your database: psql "<connection_string>" -f scripts/seed_platform.sql

BEGIN;

-- 1) Platform roles
INSERT INTO "PlatformRoles" ("Id","Name","Code","Description","IsActive","CreatedAt","CreatedBy") VALUES
  ('role-admin-00000000000000000001','系統管理員','admin','系統超級管理員，擁有全部權限', true, extract(epoch from now())::bigint, 'seed');

-- 2) Platform permissions (examples)
INSERT INTO "PlatformPermissions" ("Id","Code","Name","GroupName","Description","CreatedAt","CreatedBy") VALUES
  ('perm-faq-manage-0000000001','faq.manage','管理 FAQ','FAQ','Create/Edit/Delete FAQs', extract(epoch from now())::bigint, 'seed'),
  ('perm-users-manage-00000002','users.manage','管理後台帳號','Users','Create/Edit/Delete Platform Users', extract(epoch from now())::bigint, 'seed'),
  ('perm-roles-manage-00000003','roles.manage','管理角色與權限','Admin','Manage roles and permissions', extract(epoch from now())::bigint, 'seed'),
  ('perm-system-settings-0004','system.settings','系統設定管理','System','Update system settings', extract(epoch from now())::bigint, 'seed');

-- 3) Assign all permissions to admin role
INSERT INTO "PlatformRolePermissions" ("Id","PlatformRoleId","PlatformPermissionId","CreatedAt","CreatedBy") VALUES
  ('roleperm-0001','role-admin-00000000000000000001','perm-faq-manage-0000000001', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-0002','role-admin-00000000000000000001','perm-users-manage-00000002', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-0003','role-admin-00000000000000000001','perm-roles-manage-00000003', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-0004','role-admin-00000000000000000001','perm-system-settings-0004', extract(epoch from now())::bigint, 'seed');

-- 4) Platform user (example admin account)
-- NOTE: current code stores passwords in PlatformUser.PasswordHash as plaintext.
-- Use a secure password and change this after first login; recommended to implement hashing.
INSERT INTO "PlatformUsers" ("Id","Name","Email","PasswordHash","Department","JobTitle","Phone","IsActive","CreatedAt","CreatedBy") VALUES
  ('platuser-admin-000000001','總部管理員','admin@example.com','changeme','Engineering','Administrator','+886-2-1234-5678', true, extract(epoch from now())::bigint, 'seed');

-- 5) Assign user to admin role
INSERT INTO "PlatformUserRoles" ("Id","PlatformUserId","PlatformRoleId","CreatedAt","CreatedBy") VALUES
  ('urol-0001','platuser-admin-000000001','role-admin-00000000000000000001', extract(epoch from now())::bigint, 'seed');

-- 6) Example system settings (optional)
INSERT INTO "SystemSettings" ("Id","SettingKey","SettingValue","Description","UpdatedAt","UpdatedBy") VALUES
  ('ss-openai-0001','Embedding:OpenAiApiKey','','OpenAI API key for embeddings (set in env or replace here)', extract(epoch from now())::bigint, 'seed'),
  ('ss-msg-webhook-0001','MessageApi:WebhookUrl','','Webhook URL for Message API tests', extract(epoch from now())::bigint, 'seed');

COMMIT;

-- End of seed

-- ==================================================================
-- Permissions for Admin controllers (merged from seed_platform_permissions_admin.sql)
-- NOTE: this block assumes the admin role id 'role-admin-00000000000000000001' exists above
-- Insert one permission per Admin controller
INSERT INTO "PlatformPermissions" ("Id","Code","Name","GroupName","Description","CreatedAt","CreatedBy") VALUES
  ('perm-admin-01','admin.account','Admin:Account','Admin','Access and manage account/login pages', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-02','admin.aifaqanalysisjobs','Admin:AiFaqAnalysisJobs','Admin','Access and manage AI FAQ analysis jobs', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-03','admin.analytics','Admin:Analytics','Admin','View analytics and persona settings', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-04','admin.audit','Admin:Audit','Admin','Access audit and governance pages', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-05','admin.auditlogs','Admin:AuditLogs','Admin','View audit logs', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-06','admin.bulkjobs','Admin:BulkJobs','Admin','Manage bulk jobs', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-07','admin.conversations','Admin:Conversations','Admin','View and manage conversations', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-08','admin.conversationstates','Admin:ConversationStates','Admin','Manage conversation states', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-09','admin.embeddingjobs','Admin:EmbeddingJobs','Admin','Manage embedding jobs', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-10','admin.faqaliases','Admin:FaqAliases','FAQ','Manage FAQ aliases', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-11','admin.faqanalysis','Admin:FaqAnalysis','FAQ','Review FAQ analysis items', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-12','admin.faqlogs','Admin:FaqLogs','FAQ','View FAQ logs', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-13','admin.faqquerylogs','Admin:FaqQueryLogs','FAQ','View FAQ query logs', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-14','admin.files','Admin:Files','Admin','Manage uploaded files', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-15','admin.groups','Admin:Groups','Admin','Manage groups', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-16','admin.home','Admin:Home','Admin','Access admin dashboard', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-17','admin.knowledgecandidates','Admin:KnowledgeCandidates','Knowledge','Manage knowledge candidates', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-18','admin.messageapi','Admin:MessageApi','Messaging','Manage Message API settings', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-19','admin.messageresults','Admin:MessageResults','Messaging','View message results', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-20','admin.messageroutes','Admin:MessageRoutes','Messaging','Manage message routes', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-21','admin.platformpermissions','Admin:PlatformPermissions','Admin','Manage platform permissions', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-22','admin.platformroles','Admin:PlatformRoles','Admin','Manage platform roles', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-23','admin.platformusers','Admin:PlatformUsers','Admin','Manage platform users', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-24','admin.summaries','Admin:Summaries','Admin','View summaries and exports', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-25','admin.systemsettings','Admin:SystemSettings','System','Manage system settings', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-26','admin.vendoraccounts','Admin:VendorAccounts','Vendors','Manage vendor accounts', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-27','admin.vendors','Admin:Vendors','Vendors','Manage vendors', extract(epoch from now())::bigint, 'seed'),
  ('perm-admin-28','admin.vendorstaffusers','Admin:VendorStaffUsers','Vendors','Manage vendor staff users', extract(epoch from now())::bigint, 'seed');

-- Assign all created permissions to admin role
INSERT INTO "PlatformRolePermissions" ("Id","PlatformRoleId","PlatformPermissionId","CreatedAt","CreatedBy") VALUES
  ('roleperm-admin-01','role-admin-00000000000000000001','perm-admin-01', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-02','role-admin-00000000000000000001','perm-admin-02', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-03','role-admin-00000000000000000001','perm-admin-03', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-04','role-admin-00000000000000000001','perm-admin-04', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-05','role-admin-00000000000000000001','perm-admin-05', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-06','role-admin-00000000000000000001','perm-admin-06', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-07','role-admin-00000000000000000001','perm-admin-07', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-08','role-admin-00000000000000000001','perm-admin-08', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-09','role-admin-00000000000000000001','perm-admin-09', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-10','role-admin-00000000000000000001','perm-admin-10', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-11','role-admin-00000000000000000001','perm-admin-11', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-12','role-admin-00000000000000000001','perm-admin-12', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-13','role-admin-00000000000000000001','perm-admin-13', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-14','role-admin-00000000000000000001','perm-admin-14', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-15','role-admin-00000000000000000001','perm-admin-15', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-16','role-admin-00000000000000000001','perm-admin-16', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-17','role-admin-00000000000000000001','perm-admin-17', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-18','role-admin-00000000000000000001','perm-admin-18', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-19','role-admin-00000000000000000001','perm-admin-19', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-20','role-admin-00000000000000000001','perm-admin-20', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-21','role-admin-00000000000000000001','perm-admin-21', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-22','role-admin-00000000000000000001','perm-admin-22', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-23','role-admin-00000000000000000001','perm-admin-23', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-24','role-admin-00000000000000000001','perm-admin-24', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-25','role-admin-00000000000000000001','perm-admin-25', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-26','role-admin-00000000000000000001','perm-admin-26', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-27','role-admin-00000000000000000001','perm-admin-27', extract(epoch from now())::bigint, 'seed'),
  ('roleperm-admin-28','role-admin-00000000000000000001','perm-admin-28', extract(epoch from now())::bigint, 'seed');

-- End Merge

-- Assign a specific PlatformUser to admin role (example user from Supabase UI)
INSERT INTO "PlatformUserRoles" ("Id","PlatformUserId","PlatformRoleId","CreatedAt","CreatedBy") VALUES
  ('urol-0002','4cf91e79baec4b3591b350fe279101e','role-admin-00000000000000000001', extract(epoch from now())::bigint, 'seed');

