using CRMSecurityProvider.Sources.Attribute;
using CRMSecurityProvider.Sources.Entity;
using Sitecore.CrmCampaignIntegration.Core;
using Sitecore.CrmCampaignIntegration.Core.Crm;
using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.Form.Core.Utility;
using Sitecore.Forms.Core.Data;
using Sitecore.StringExtensions;
using Sitecore.WFFM.Abstractions.Actions;
using Sitecore.WFFM.Abstractions.Dependencies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Services.Protocols;

namespace Sitecore.Support.CrmCampaignIntegration.Submit
{
    public class CreateCrmEntity : Sitecore.CrmCampaignIntegration.Submit.CreateCrmEntity
    {
        protected new AdaptedResultList Result { get; private set; }
        private ID formId;
        private bool isCreated;


        public override FormItem CurrentForm
        {
            get
            {
                if (!ID.IsNullOrEmpty(this.formId))
                {
                    return FormItem.GetForm(this.formId);
                }
                return null;
            }
        }


        private string[] GetColumns()
        {
            List<string> second = new List<string> {
        this.EntitySettings.PrimaryKey,
        this.EntitySettings.PrimaryFieldName
    };
            if (this.EntitySettings.Audit != "NoAudit")
            {
                second.Add(this.EntitySettings.Audit);
            }
            return (from f in this.EntitySettings.Fields select f.Name).Union<string>(second).ToArray<string>();
        }

        private string GetValueSource(XCrmField field, AdaptedResultList list)
        {
            switch (field.UseValueType)
            {
                case 0:
                    {
                        AdaptedControlResult entryByID = list.GetEntryByID(field.FormValueFrom);
                        return entryByID?.FieldName;
                    }
                case 1:
                    return "crm";

                case 2:
                    return "manual";

                case 3:
                    {
                        FormItem currentForm = this.CurrentForm;
                        if (currentForm == null)
                        {
                            break;
                        }
                        IActionItem item2 = DependenciesManager.ActionExecutor.GetAcitonByUniqId(currentForm, field.CrmValue, true);
                        if (item2 == null)
                        {
                            break;
                        }
                        return "previous action: {0}".FormatWith(new object[] { item2.DisplayName });
                    }
                default:
                    return string.Empty;
            }
            return "previous action: {0}".FormatWith(new object[] { field.CrmValue });
        }






        protected override void SetProperties(ICrmEntity entity, AdaptedResultList fields)
        {
            if ((entity != null) && (fields != null))
            {
                foreach (XCrmField field in this.EntitySettings.Fields)
                {
                    string str = this.GetValue(field, fields);
                    if ((field.AttributeType != CrmAttributeType.Picklist) || !string.IsNullOrEmpty(str))
                    {
                        if (str == null)
                        {
                            Log.Warn("'Create crm {0}' action: the {1} field requires some more settings defined.".FormatWith(new object[] { this.EntitySettings.EntityName, field.Name }), this);
                        }
                        else
                        {
                            string propertyValue = this.GetPropertyValue(entity, field.Name);
                            bool flag = fields.IsTrueStatement(field.EditMode);
                            if (flag && (this.EntitySettings.OverwriteNotEmptyField || string.IsNullOrEmpty(propertyValue)))
                            {
                                this.SetProperty(field.Name, field.AttributeType, str, entity, new string[] { field.EntityReference, this.EntitySettings.EntityName, this.EntitySettings.PrimaryKey });
                            }
                            if ((string.Compare(str, propertyValue, true) != 0) || this.isCreated)
                            {
                                if (flag && (this.EntitySettings.OverwriteNotEmptyField || string.IsNullOrEmpty(propertyValue)))
                                {
                                    base.AuditUpdatedField(this.GetValueSource(field, fields), field.Name, str);
                                }
                                else
                                {
                                    base.AuditSkippedField(this.GetValueSource(field, fields), field.Name, str);
                                }
                            }
                        }
                    }
                }
                if (this.IsAuditEnabled)
                {
                    string str3 = base.DumpAuditInfomration(this.GetPropertyValue(entity, this.EntitySettings.Audit));
                    if (!string.IsNullOrEmpty(str3))
                    {
                        this.SetProperty(this.EntitySettings.Audit, this.EntitySettings.AuditAttributeType, str3, entity, new string[0]);
                    }
                }
            }
        }





        public override void Execute(ID formId, AdaptedResultList adaptedFields, ActionCallContext actionCallContext = null, params object[] data)
        {
            Assert.ArgumentNotNull(formId, "formId");
            Assert.ArgumentNotNull(adaptedFields, "fields");
            this.Result = adaptedFields;
            this.formId = formId;
            Guid empty = Guid.Empty;
            try
            {
                if ((this.EntitySettings != null) && ((this.PrimaryField != null) || !this.CanBeOverwritten))
                {
                    Guid guid2;
                    ICrmEntity entity = null;
                    if (this.CanBeOverwritten)
                    {
                        string str = this.GetValue(this.PrimaryField, adaptedFields);
                        if (string.IsNullOrEmpty(str))
                        {
                            throw new ArgumentException(this.KeyFieldUndefinedMessage);
                        }
                        entity = this.Get(this.EntityName, this.EntitySettings.PrimaryFieldName, str, this.EntitySettings.SupportStateCode, this.GetColumns());
                    }
                    if (entity == null)
                    {
                        entity = base.EntityRepository.NewEntity(this.EntityName);
                        this.isCreated = true;
                        this.InitEntityState(entity);
                        this.SetProperties(entity, adaptedFields);
                        this.SetCustomCrmProperties(this.formId, adaptedFields, entity);
                        empty = this.Create(entity);
                        guid2 = empty;
                        FormsCrmEntity entity2 = new FormsCrmEntity
                        {
                            Name = this.EntitySettings.EntityName,
                            ID = guid2
                        };
                        actionCallContext.Parameters.Add(base.UniqueKey, entity2);
                        if (Guid.Empty == empty)
                        {
                            throw new InvalidOperationException(this.CannotBeCreatedMessage);
                        }
                    }
                    else
                    {
                        guid2 = new Guid(this.GetPropertyValue(entity, this.EntitySettings.PrimaryKey));
                        this.SetProperties(entity, adaptedFields);
                        this.SetCustomCrmProperties(this.formId, adaptedFields, entity);
                        this.Update(entity);
                        FormsCrmEntity entity3 = new FormsCrmEntity
                        {
                            Name = this.EntitySettings.EntityName,
                            ID = guid2
                        };
                        actionCallContext.Parameters.Add(base.UniqueKey, entity3);
                    }
                }
                else
                {
                    Log.Warn("'The Create CRM {0}' action is not customized.".FormatWith(new object[] { (this.EntitySettings != null) ? this.EntitySettings.EntityName : "entity" }), this);
                }
            }
            catch (SoapException exception)
            {
                Exception exception2 = new Exception(exception.GetFormatedMessage(), exception);
                this.UndoAction(empty);
                throw exception2;
            }
            catch (Exception)
            {
                this.UndoAction(empty);
                throw;
            }
        }


        private void UndoAction(Guid entityId)
        {
            if (entityId != Guid.Empty)
            {
                base.EntityRepository.Delete(this.EntitySettings.EntityName, entityId);
            }
        }
    }
}
