/*
Copyright 2017 YANG Huan (sy.yanghuan@gmail.com).

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

  http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpLua.LuaAst {
  public sealed class LuaSpeaicalGenericType {
    public LuaIdentifierNameSyntax Name;
    public LuaExpressionSyntax Value;
    public bool IsLazy;
  }

  public sealed class LuaConstructorDeclaration {
    public LuaConstructorAdapterExpressionSyntax Function;
    public LuaDocumentStatement Document;
  }

  public abstract class LuaTypeDeclarationSyntax : LuaWrapFunctionStatementSynatx {
    public bool IsPartialMark { get; set; }
    public bool IsClassUsed { get; set; }
    protected bool isStruct_;
    public bool IsForceStaticCtor { get; set; }

    private LuaLocalAreaSyntax local_ = new LuaLocalAreaSyntax();
    private List<LuaTypeDeclarationSyntax> nestedTypeDeclarations_ = new List<LuaTypeDeclarationSyntax>();
    private LuaStatementListSyntax methodList_ = new LuaStatementListSyntax();
    private LuaTableExpression resultTable_ = new LuaTableExpression();

    private List<LuaStatementSyntax> staticInitStatements_ = new List<LuaStatementSyntax>();
    private List<LuaIdentifierNameSyntax> staticAssignmentNames_ = new List<LuaIdentifierNameSyntax>();
    private LuaConstructorDeclaration staticCtor_;

    private List<LuaStatementSyntax> initStatements_ = new List<LuaStatementSyntax>();
    private List<LuaConstructorDeclaration> ctors_ = new List<LuaConstructorDeclaration>();

    private List<LuaParameterSyntax> typeParameters_ = new List<LuaParameterSyntax>();
    private List<GenericUsingDeclare> genericUsingDeclares_ = new List<GenericUsingDeclare>();

    private LuaTableExpression metadata_;
    private LuaTableExpression metaProperties_;
    private LuaTableExpression metaEvents_;
    private LuaTableExpression metaFields_;
    private LuaTableExpression metaMethods_;
    private LuaDocumentStatement document_ = new LuaDocumentStatement();
    public bool IsIgnoreExport => document_.HasIgnoreAttribute;
    public bool IsExportMetadata => document_.HasMetadataAttribute;
    public bool IsExportMetadataAll => document_.HasMetadataAllAttribute;

    internal void AddStaticReadOnlyAssignmentName(LuaIdentifierNameSyntax name) {
      if (!staticAssignmentNames_.Contains(name)) {
        staticAssignmentNames_.Add(name);
      }
    }

    internal void AddDocument(LuaDocumentStatement document) {
      if (document != null) {
        document_.Add(document);
      }
    }

    internal void AddNestedTypeDeclaration(LuaTypeDeclarationSyntax typeDeclaration) {
      nestedTypeDeclarations_.Add(typeDeclaration);
    }

    private void AddMetadata(ref LuaTableExpression table, LuaIdentifierNameSyntax name, LuaTableItemSyntax item) {
      if (metadata_ == null) {
        metadata_ = new LuaTableExpression();
      }
      if (table == null) {
        table = new LuaTableExpression();
        metadata_.Add(name, table);
      }
      table.Items.Add(item);
    }

    internal void AddClassMetaData(LuaTableExpression data) {
      if (metadata_ == null) {
        metadata_ = new LuaTableExpression();
      }
      metadata_.Add(LuaIdentifierNameSyntax.Class, data);
    }

    internal void AddFieldMetaData(LuaTableExpression data) {
      AddMetadata(ref metaFields_, LuaIdentifierNameSyntax.Fields, new LuaSingleTableItemSyntax(data));
    }

    internal void AddPropertyMetaData(LuaTableExpression data) {
      AddMetadata(ref metaProperties_, LuaIdentifierNameSyntax.Properties, new LuaSingleTableItemSyntax(data));
    }

    internal void AddEventMetaData(LuaTableExpression data) {
      AddMetadata(ref metaEvents_, LuaIdentifierNameSyntax.Events, new LuaSingleTableItemSyntax(data));
    }

    internal void AddMethodMetaData(LuaTableExpression data) {
      AddMetadata(ref metaMethods_, LuaIdentifierNameSyntax.Methods, new LuaSingleTableItemSyntax(data));
    }

    internal void AddTypeParameters(IEnumerable<LuaParameterSyntax> typeParameters) {
      typeParameters_.AddRange(typeParameters);
    }

    internal void AddImport(LuaInvocationExpressionSyntax invocationExpression, string name, List<string> argumentTypeNames, bool isFromCode) {
      GenericUsingDeclare.AddImportTo(genericUsingDeclares_, invocationExpression, name, argumentTypeNames, isFromCode);
    }

    internal void AddBaseTypes(IEnumerable<LuaExpressionSyntax> baseTypes, LuaSpeaicalGenericType genericArgument, List<LuaIdentifierNameSyntax> baseCopyFields) {
      bool hasLazyGenericArgument = false;
      if (genericArgument != null) {
        if (genericArgument.IsLazy) {
          hasLazyGenericArgument = true;
        } else {
          AddResultTable(genericArgument.Name, genericArgument.Value);
        }
      }

      bool hasBaseCopyField = baseCopyFields != null && baseCopyFields.Count > 0;
      LuaFunctionExpressionSyntax functionExpression = new LuaFunctionExpressionSyntax();
      functionExpression.AddParameter(LuaIdentifierNameSyntax.Global);
      if (hasLazyGenericArgument || hasBaseCopyField) {
        functionExpression.AddParameter(LuaIdentifierNameSyntax.This);
      }

      if (hasLazyGenericArgument) {
        var assignment = new LuaAssignmentExpressionSyntax(new LuaMemberAccessExpressionSyntax(LuaIdentifierNameSyntax.This, genericArgument.Name), genericArgument.Value);
        functionExpression.AddStatement(assignment);
      }

      LuaTableExpression table = new LuaTableExpression();
      if (hasBaseCopyField) {
        var baseIdentifier = LuaIdentifierNameSyntax.Base;
        functionExpression.AddStatement(new LuaLocalVariableDeclaratorSyntax(baseIdentifier, baseTypes.First()));
        foreach (var field in baseCopyFields) {
          var left = new LuaMemberAccessExpressionSyntax(LuaIdentifierNameSyntax.This, field);
          var right = new LuaMemberAccessExpressionSyntax(baseIdentifier, field);
          functionExpression.AddStatement(new LuaAssignmentExpressionSyntax(left, right));

          table.Items.Add(new LuaSingleTableItemSyntax(baseIdentifier));
          table.Items.AddRange(baseTypes.Skip(1).Select(i => new LuaSingleTableItemSyntax(i)));
        }
      } else {
        table.Items.AddRange(baseTypes.Select(i => new LuaSingleTableItemSyntax(i)));
      }

      functionExpression.AddStatement(new LuaReturnStatementSyntax(table));
      AddResultTable(LuaIdentifierNameSyntax.Inherits, functionExpression);
    }

    private void AddResultTable(LuaIdentifierNameSyntax name) {
      LuaKeyValueTableItemSyntax item = new LuaKeyValueTableItemSyntax(name, name);
      resultTable_.Items.Add(item);
    }

    private void AddResultTable(LuaIdentifierNameSyntax name, LuaExpressionSyntax value) {
      AddResultTable(new LuaKeyValueTableItemSyntax(name, value));
    }

    protected void AddResultTable(LuaKeyValueTableItemSyntax item) {
      resultTable_.Items.Add(item);
    }

    public void AddMethod(LuaIdentifierNameSyntax name, LuaFunctionExpressionSyntax method, bool isPrivate, LuaDocumentStatement document = null) {
      if (document != null && document.HasIgnoreAttribute) {
        return;
      }

      local_.Variables.Add(name);
      LuaAssignmentExpressionSyntax assignment = new LuaAssignmentExpressionSyntax(name, method);
      if (document != null && !document.IsEmpty) {
        methodList_.Statements.Add(document);
      }
      methodList_.Statements.Add(new LuaExpressionStatementSyntax(assignment));
      if (!isPrivate) {
        AddResultTable(name);
      }
    }

    private void AddInitFiled(LuaIdentifierNameSyntax name, LuaExpressionSyntax value) {
      LuaMemberAccessExpressionSyntax memberAccess = new LuaMemberAccessExpressionSyntax(LuaIdentifierNameSyntax.This, name);
      LuaAssignmentExpressionSyntax assignment = new LuaAssignmentExpressionSyntax(memberAccess, value);
      initStatements_.Add(new LuaExpressionStatementSyntax(assignment));
    }

    public void AddField(LuaIdentifierNameSyntax name, LuaExpressionSyntax value, bool isImmutable, bool isStatic, bool isPrivate, bool isReadOnly, List<LuaStatementSyntax> statements) {
      if (isStatic) {
        if (isPrivate) {
          local_.Variables.Add(name);
          if (value != null) {
            LuaAssignmentExpressionSyntax assignment = new LuaAssignmentExpressionSyntax(name, value);
            if (isImmutable) {
              methodList_.Statements.Add(new LuaExpressionStatementSyntax(assignment));
            } else {
              if (statements != null) {
                staticInitStatements_.AddRange(statements);
              }
              staticInitStatements_.Add(new LuaExpressionStatementSyntax(assignment));
            }
          }
        } else {
          if (isReadOnly) {
            local_.Variables.Add(name);
            if (value != null) {
              if (isImmutable) {
                LuaAssignmentExpressionSyntax assignment = new LuaAssignmentExpressionSyntax(name, value);
                methodList_.Statements.Add(new LuaExpressionStatementSyntax(assignment));
                AddResultTable(name);
              } else {
                if (statements != null) {
                  staticInitStatements_.AddRange(statements);
                }
                var assignment = new LuaAssignmentExpressionSyntax(name, value);
                staticInitStatements_.Add(new LuaExpressionStatementSyntax(assignment));

                var memberAccess = new LuaMemberAccessExpressionSyntax(LuaIdentifierNameSyntax.This, name);
                staticInitStatements_.Add(new LuaAssignmentExpressionSyntax(memberAccess, name));
              }
            }
          } else {
            if (value != null) {
              if (isImmutable) {
                AddResultTable(name, value);
              } else {
                if (statements != null) {
                  staticInitStatements_.AddRange(statements);
                }
                var assignment = new LuaAssignmentExpressionSyntax(new LuaMemberAccessExpressionSyntax(LuaIdentifierNameSyntax.This, name), value);
                staticInitStatements_.Add(new LuaExpressionStatementSyntax(assignment));
              }
            }
          }
        }
      } else {
        if (value != null) {
          if (isImmutable) {
            AddResultTable(name, value);
          } else {
            if (statements  != null) {
              initStatements_.AddRange(statements);
            }
            AddInitFiled(name, value);
          }
        }
      }
    }

    private (LuaPropertyOrEventIdentifierNameSyntax, LuaPropertyOrEventIdentifierNameSyntax) AddPropertyOrEvent(bool isProperty, LuaIdentifierNameSyntax name, LuaIdentifierNameSyntax innerName, LuaExpressionSyntax value, bool isImmutable, bool isStatic, bool isPrivate, LuaExpressionSyntax typeExpression, List<LuaStatementSyntax> statements) {
      LuaPropertyOrEventIdentifierNameSyntax get, set;
      if (isProperty) {
        get = new LuaPropertyOrEventIdentifierNameSyntax(true, true, name);
        set = new LuaPropertyOrEventIdentifierNameSyntax(true, false, name);
      } else {
        get = new LuaPropertyOrEventIdentifierNameSyntax(false, true, name);
        set = new LuaPropertyOrEventIdentifierNameSyntax(false, false, name);
      }

      local_.Variables.Add(get);
      local_.Variables.Add(set);
      if (!isStatic) {
        LuaIdentifierNameSyntax initMethodIdentifier = isProperty ? LuaIdentifierNameSyntax.Property : LuaIdentifierNameSyntax.Event;
        LuaMultipleAssignmentExpressionSyntax assignment = new LuaMultipleAssignmentExpressionSyntax();
        assignment.Lefts.Add(get);
        assignment.Lefts.Add(set);
        LuaInvocationExpressionSyntax invocation = new LuaInvocationExpressionSyntax(initMethodIdentifier);
        invocation.AddArgument(new LuaStringLiteralExpressionSyntax(innerName));
        assignment.Rights.Add(invocation);
        methodList_.Statements.Add(assignment);
      } else {
        var memberAccess = new LuaMemberAccessExpressionSyntax(typeExpression, innerName);
        var getFunction = new LuaFunctionExpressionSyntax();
        var setFunction = new LuaFunctionExpressionSyntax();
        if (isProperty) {
          getFunction.AddStatement(new LuaReturnStatementSyntax(memberAccess));
          setFunction.AddParameter(LuaIdentifierNameSyntax.Value);
          var assignment = new LuaAssignmentExpressionSyntax(memberAccess, LuaIdentifierNameSyntax.Value);
          setFunction.AddStatement(assignment);
        } else {
          getFunction.AddParameter(LuaIdentifierNameSyntax.Value);
          var getAssignment = new LuaAssignmentExpressionSyntax(memberAccess, new LuaBinaryExpressionSyntax(memberAccess, Tokens.Plus, LuaIdentifierNameSyntax.Value));
          getFunction.AddStatement(getAssignment);
          setFunction.AddParameter(LuaIdentifierNameSyntax.Value);
          var setAssignment = new LuaAssignmentExpressionSyntax(memberAccess, new LuaBinaryExpressionSyntax(memberAccess, Tokens.Sub, LuaIdentifierNameSyntax.Value));
          setFunction.AddStatement(setAssignment);
        }
        methodList_.Statements.Add(new LuaAssignmentExpressionSyntax(get, getFunction));
        methodList_.Statements.Add(new LuaAssignmentExpressionSyntax(set, setFunction));
      }

      if (value != null) {
        if (isStatic) {
          if (isImmutable) {
            AddResultTable(innerName, value);
          } else {
            if (statements != null) {
              staticInitStatements_.AddRange(statements);
            }
            LuaAssignmentExpressionSyntax assignment = new LuaAssignmentExpressionSyntax(new LuaMemberAccessExpressionSyntax(LuaIdentifierNameSyntax.This, innerName), value);
            staticInitStatements_.Add(new LuaExpressionStatementSyntax(assignment));
          }
        } else {
          if (isImmutable) {
            AddResultTable(innerName, value);
          } else {
            if (statements != null) {
              initStatements_.AddRange(statements);
            }
            AddInitFiled(innerName, value);
          }
        }
      }

      if (!isPrivate) {
        AddResultTable(get);
        AddResultTable(set);
      }

      return (get, set);
    }

    public (LuaPropertyOrEventIdentifierNameSyntax, LuaPropertyOrEventIdentifierNameSyntax) AddProperty(LuaIdentifierNameSyntax name, LuaIdentifierNameSyntax innerName, LuaExpressionSyntax value, bool isImmutable, bool isStatic, bool isPrivate, LuaExpressionSyntax typeExpression, List<LuaStatementSyntax> statements) {
      return AddPropertyOrEvent(true, name, innerName, value, isImmutable, isStatic, isPrivate, typeExpression, statements);
    }

    public (LuaPropertyOrEventIdentifierNameSyntax, LuaPropertyOrEventIdentifierNameSyntax) AddEvent(LuaIdentifierNameSyntax name, LuaIdentifierNameSyntax innerName, LuaExpressionSyntax value, bool isImmutable, bool isStatic, bool isPrivate, LuaExpressionSyntax typeExpression, List<LuaStatementSyntax> statements) {
      return AddPropertyOrEvent(false, name, innerName, value, isImmutable, isStatic, isPrivate, typeExpression, statements);
    }

    public void SetStaticCtor(LuaConstructorAdapterExpressionSyntax function, LuaDocumentStatement document) {
      Contract.Assert(staticCtor_ == null);
      staticCtor_ = new LuaConstructorDeclaration() {
        Function = function,
        Document = document,
      };
    }

    public bool IsNoneCtros {
      get {
        return ctors_.Count == 0;
      }
    }

    public bool IsInitStatementExists {
      get {
        return initStatements_.Count > 0;
      }
    }

    public void AddCtor(LuaConstructorAdapterExpressionSyntax function, bool isZeroParameters, LuaDocumentStatement document = null) {
      if (isZeroParameters) {
        ctors_.Insert(0, new LuaConstructorDeclaration() {
          Function = function,
          Document = document,
        });
      } else {
        ctors_.Add(new LuaConstructorDeclaration() {
          Function = function,
          Document = document,
        });
      }
    }

    private void AddInitFunction(LuaBlockSyntax body, LuaIdentifierNameSyntax name, LuaFunctionExpressionSyntax initFunction, bool isAddItem = true) {
      local_.Variables.Add(name);
      body.Statements.Add(new LuaAssignmentExpressionSyntax(name, initFunction));
      if (isAddItem) {
        AddResultTable(name);
      }
    }

    private void AddStaticAssignmentNames(LuaBlockSyntax body) {
      if (staticAssignmentNames_.Count > 0) {
        LuaMultipleAssignmentExpressionSyntax assignment = new LuaMultipleAssignmentExpressionSyntax();
        foreach (var identifierName in staticAssignmentNames_) {
          LuaMemberAccessExpressionSyntax memberAccess = new LuaMemberAccessExpressionSyntax(LuaIdentifierNameSyntax.This, identifierName);
          assignment.Lefts.Add(memberAccess);
          assignment.Rights.Add(identifierName);
        }
        body.Statements.Add(new LuaExpressionStatementSyntax(assignment));
      }
    }

    private void CheckStaticCtorFunction(LuaBlockSyntax body) {
      var statements = new List<LuaStatementSyntax>();
      statements.AddRange(staticInitStatements_);
      if (staticCtor_ != null) {
        statements.AddRange(staticCtor_.Function.Body.Statements);
      }
      if (statements.Count > 0 || IsForceStaticCtor) {
        var staticCtor = new LuaFunctionExpressionSyntax();
        staticCtor.AddParameter(LuaIdentifierNameSyntax.This);
        staticCtor.Body.Statements.AddRange(statements);
        AddStaticAssignmentNames(staticCtor.Body);
        if (staticCtor_ != null && staticCtor_.Document != null) {
          body.AddStatement(staticCtor_.Document);
        }
        AddInitFunction(body, LuaIdentifierNameSyntax.StaticCtor, staticCtor);
      }
    }

    private LuaFunctionExpressionSyntax GetInitFunction() {
      LuaFunctionExpressionSyntax initFuntion = new LuaFunctionExpressionSyntax();
      initFuntion.AddParameter(LuaIdentifierNameSyntax.This);
      initFuntion.Body.Statements.AddRange(initStatements_);
      return initFuntion;
    }

    private void CheckCtorsFunction(LuaBlockSyntax body) {
      bool hasInit = initStatements_.Count > 0;
      bool hasCtors = ctors_.Count > 0;

      if (hasCtors) {
        if (hasInit) {
          if (ctors_.Count == 1 || isStruct_) {
            ctors_.First().Function.Body.Statements.InsertRange(0, initStatements_);
          } else {
            var init = LuaIdentifierNameSyntax.Init;
            AddInitFunction(body, init, GetInitFunction(), false);
            foreach (var ctor in ctors_) {
              if (!ctor.Function.IsInvokeThisCtor) {
                var invocationInit = new LuaInvocationExpressionSyntax(init, LuaIdentifierNameSyntax.This);
                ctor.Function.Body.Statements.Insert(0, new LuaExpressionStatementSyntax(invocationInit));
              }
            }
          }
        }

        if (ctors_.Count == 1) {
          var ctor = ctors_.First();
          if (ctor.Function.Body.Statements.Count > 0) {
            if (ctor.Document != null) {
              body.AddStatement(ctor.Document);
            }
            AddInitFunction(body, LuaIdentifierNameSyntax.Ctor, ctor.Function);
          }
        } else {
          LuaTableExpression ctrosTable = new LuaTableExpression();
          int index = 1;
          foreach (var ctor in ctors_) {
            if (ctor.Document != null) {
              body.AddStatement(ctor.Document);
            }
            LuaIdentifierNameSyntax nameIdentifier = GetCtorNameString(index);
            AddInitFunction(body, nameIdentifier, ctor.Function, false);
            ctrosTable.Add(nameIdentifier);
            ++index;
          }
          AddResultTable(LuaIdentifierNameSyntax.Ctor, ctrosTable);
        }
      } else {
        if (hasInit) {
          AddInitFunction(body, LuaIdentifierNameSyntax.Ctor, GetInitFunction());
        }
      }
    }

    private static string MetaDataName(LuaTableItemSyntax item) {
      var table = (LuaTableExpression)(((LuaSingleTableItemSyntax)item).Expression);
      var name = (LuaStringLiteralExpressionSyntax)((LuaSingleTableItemSyntax)table.Items.First()).Expression;
      return name.Text;
    }

    private static void SortMetaData(LuaTableExpression metaData) {
      if (metaData != null) {
        metaData.Items.Sort((x, y) => MetaDataName(x).CompareTo(MetaDataName(y)));
      }
    }

    private void CheckMetadata() {
      if (metadata_ != null) {
        SortMetaData(metaFields_);
        SortMetaData(metaProperties_);
        SortMetaData(metaMethods_);
        LuaFunctionExpressionSyntax functionExpression = new LuaFunctionExpressionSyntax();
        functionExpression.AddParameter(LuaIdentifierNameSyntax.Global);
        functionExpression.AddStatement(new LuaReturnStatementSyntax(metadata_));
        AddResultTable(LuaIdentifierNameSyntax.Metadata, functionExpression);
      }
    }

    private void CheckGenericUsingDeclares(LuaBlockSyntax body) {
      if (genericUsingDeclares_.Count > 0) {
        genericUsingDeclares_.Sort();
        foreach (var import in genericUsingDeclares_) {
          body.AddStatement(new LuaLocalVariableDeclaratorSyntax(import.NewName, import.InvocationExpression));
        }
      }
    }

    private void AddAllStatementsTo(LuaBlockSyntax body) {
      body.Statements.Add(local_);
      body.Statements.AddRange(nestedTypeDeclarations_);
      CheckGenericUsingDeclares(body);
      CheckStaticCtorFunction(body);
      CheckCtorsFunction(body);
      body.Statements.Add(methodList_);
      CheckMetadata();
      if (IsClassUsed) {
        body.Statements.Add(new LuaAssignmentExpressionSyntax(LuaIdentifierNameSyntax.Class, resultTable_));
        body.Statements.Add(new LuaReturnStatementSyntax(LuaIdentifierNameSyntax.Class));
      } else {
        body.Statements.Add(new LuaReturnStatementSyntax(resultTable_));
      }
    }

    internal override void Render(LuaRenderer renderer) {
      if (IsPartialMark) {
        return;
      }

      if (IsIgnoreExport) {
        return;
      }

      if (IsClassUsed) {
        local_.Variables.Add(LuaIdentifierNameSyntax.Class);
      }

      document_.Render(renderer);
      if (typeParameters_.Count > 0) {
        AddStatements(nestedTypeDeclarations_);
        nestedTypeDeclarations_.Clear();
        LuaFunctionExpressionSyntax wrapFunction = new LuaFunctionExpressionSyntax();
        foreach (var type in typeParameters_) {
          wrapFunction.AddParameter(type);
        }
        AddAllStatementsTo(wrapFunction.Body);
        Body.Statements.Add(new LuaReturnStatementSyntax(wrapFunction));
      } else {
        AddAllStatementsTo(Body);
      }
      base.Render(renderer);
    }
  }

  public sealed class LuaClassDeclarationSyntax : LuaTypeDeclarationSyntax {
    public LuaClassDeclarationSyntax(LuaIdentifierNameSyntax name) {
      UpdateIdentifiers(name, LuaIdentifierNameSyntax.Namespace, LuaIdentifierNameSyntax.Class, LuaIdentifierNameSyntax.Namespace);
    }
  }

  public sealed class LuaStructDeclarationSyntax : LuaTypeDeclarationSyntax {
    public LuaStructDeclarationSyntax(LuaIdentifierNameSyntax name) {
      isStruct_ = true;
      UpdateIdentifiers(name, LuaIdentifierNameSyntax.Namespace, LuaIdentifierNameSyntax.Struct, LuaIdentifierNameSyntax.Namespace);
    }
  }

  public sealed class LuaInterfaceDeclarationSyntax : LuaTypeDeclarationSyntax {
    public LuaInterfaceDeclarationSyntax(LuaIdentifierNameSyntax name) {
      UpdateIdentifiers(name, LuaIdentifierNameSyntax.Namespace, LuaIdentifierNameSyntax.Interface);
    }
  }

  public sealed class LuaEnumDeclarationSyntax : LuaTypeDeclarationSyntax {
    public string FullName { get; }
    public LuaCompilationUnitSyntax CompilationUnit { get; }
    public bool IsExport { get; set; }

    public LuaEnumDeclarationSyntax(string fullName, LuaIdentifierNameSyntax name, LuaCompilationUnitSyntax compilationUnit) {
      FullName = fullName;
      CompilationUnit = compilationUnit;
      UpdateIdentifiers(name, LuaIdentifierNameSyntax.Namespace, LuaIdentifierNameSyntax.Enum);
    }

    public void Add(LuaKeyValueTableItemSyntax statement) {
      AddResultTable(statement);
    }

    internal override void Render(LuaRenderer renderer) {
      if (IsExport) {
        base.Render(renderer);
      }
    }
  }
}
