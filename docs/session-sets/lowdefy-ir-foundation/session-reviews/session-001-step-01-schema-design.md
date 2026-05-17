The design of the `ModalDetailForm` schema prioritizes strictness, clarity for downstream code generation, and direct mapping of concepts from the cross-provider synthesis.

**Key Design Decisions:**

*   **Top-Level Structure**: The schema is organized into `metadata`, `state`, `requests`, `events`, and a single root `layout` block. This provides a clean separation of concerns. `requests` is an object (a map of names to definitions) rather than an array, enabling simple, named lookups from layout blocks and actions (e.g., `binding: "mainQuery.ClientID"`).
*   **Expression Trees (`_operator`)**: The operator catalog is implemented as a recursive schema definition (`$defs/expression`). An expression can be a constant, a state reference (`_state`), a request output reference (`_request`), or an operator call. Operator calls are objects with a single key (the operator name, e.g., `_iif`) whose value is an array of arguments. This structure is easily parsable into an expression tree. Arity is strictly enforced for each operator using `minItems` and `maxItems` on the argument array. This prevents schema validation of an `_iif` operator with two arguments, for instance.
*   **Event Hygiene (`try`/`catch`, `debounce`)**: Instead of modeling these as action types, they are modeled as optional wrappers around the primary `actions` array within an event definition. This treats them as metadata *about* the action sequence, which maps cleanly to T4 codegen wrapping a C# method body in a `try/catch` block or applying a debouncing mechanism to an event handler.
*   **Layout as a Tree**: The `layout` property is a single root block, typically a container like `Section`. This enforces a single-entry tree structure, simplifying the generator's traversal logic compared to a flat list of controls. Container blocks (`Section`, `Row`, `Column`, `Group`) recursively contain an array of blocks, defining the visual hierarchy.
*   **Target Idioms (Blazor/MudBlazor)**: The control catalog (`TextBox`, `ComboBox`, `Button`) is a minimal set directly mappable to standard MudBlazor components. Properties like `label`, `placeholder`, and event names (`onClick`, `onValueChanged`) are chosen to align with Blazor component parameters, minimizing impedance mismatch for the T4 generator. The `CallMethod` action is the designated escape hatch, directly supporting the Blazor partial class pattern for custom logic. The `_ref` mechanism is noted as a pre-processing step, ensuring this schema operates on a fully resolved, self-contained IR document, which is simpler for a T4 template to consume.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "ModalDetailForm IR",
  "description": "Schema for a first-class Intermediate Representation of an Access Modal Detail Form, intended for code generation into a Blazor component.",
  "type": "object",
  "properties": {
    "pageType": {
      "description": "The first-class type discriminator for this IR file.",
      "type": "string",
      "const": "ModalDetailForm"
    },
    "id": {
      "description": "A unique identifier for this form, typically derived from the source Access object name.",
      "type": "string"
    },
    "metadata": {
      "description": "High-level metadata about the form.",
      "type": "object",
      "properties": {
        "title": {
          "description": "The title of the form, used for window captions or page headers.",
          "type": "string"
        },
        "modalType": {
          "description": "Determines how the form is presented in the UI.",
          "type": "string",
          "enum": ["page", "modal", "drawer"]
        }
      },
      "required": ["title", "modalType"],
      "additionalProperties": false
    },
    "state": {
      "description": "A dictionary of page-level state variables and their initial values. Can be read via the `_state` operator and modified with the `SetState` action.",
      "type": "object",
      "additionalProperties": {
        "type": ["string", "number", "boolean", "null", "object", "array"]
      }
    },
    "requests": {
      "description": "A dictionary of named data requests (queries and commands). These are referenced by name from layout bindings and actions.",
      "type": "object",
      "additionalProperties": {
        "$ref": "#/$defs/request"
      }
    },
    "events": {
      "description": "A dictionary of named event handlers, mapping UI events or lifecycle hooks to sequences of actions.",
      "type": "object",
      "additionalProperties": {
        "$ref": "#/$defs/event"
      }
    },
    "layout": {
      "description": "The root layout block that defines the form's UI structure. This must be a single container block.",
      "$ref": "#/$defs/block"
    }
  },
  "required": ["pageType", "id", "metadata", "requests", "events", "layout"],
  "additionalProperties": false,
  "$defs": {
    "request": {
      "description": "Defines a data request, which references a named query or command external to this IR file.",
      "type": "object",
      "properties": {
        "queryName": {
          "description": "The name of the external query or command to execute.",
          "type": "string"
        },
        "parameters": {
          "description": "A dictionary of parameters to pass to the query. Values can be static or dynamic expressions.",
          "type": "object",
          "additionalProperties": {
            "$ref": "#/$defs/expression"
          }
        }
      },
      "required": ["queryName"],
      "additionalProperties": false
    },
    "event": {
      "description": "Defines an ordered sequence of actions triggered by an event, with optional hygiene wrappers.",
      "type": "object",
      "properties": {
        "actions": {
          "description": "The primary sequence of actions to execute.",
          "type": "array",
          "items": {
            "$ref": "#/$defs/action"
          }
        },
        "tryCatch": {
          "description": "Wraps the action sequence in a try/catch block.",
          "type": "object",
          "properties": {
            "tryActions": {
              "description": "The actions to execute within the 'try' block. If omitted, the top-level 'actions' are used.",
              "type": "array",
              "items": {
                "$ref": "#/$defs/action"
              }
            },
            "catchActions": {
              "description": "The actions to execute if an error occurs in the 'try' block.",
              "type": "array",
              "items": {
                "$ref": "#/$defs/action"
              }
            }
          },
          "required": ["catchActions"],
          "additionalProperties": false
        },
        "debounce": {
          "description": "Debounces the event, ensuring the actions are not executed until a certain time has passed without the event firing again.",
          "type": "object",
          "properties": {
            "delayMs": {
              "type": "integer",
              "minimum": 0
            }
          },
          "required": ["delayMs"],
          "additionalProperties": false
        }
      },
      "additionalProperties": false
    },
    "action": {
      "description": "A single operation to be performed as part of an event sequence.",
      "type": "object",
      "discriminator": {
        "propertyName": "type"
      },
      "oneOf": [
        {
          "properties": { "type": { "const": "Validate" }, "params": { "type": "object", "properties": { "rules": { "type": "array", "items": { "type": "object", "properties": { "expression": { "$ref": "#/$defs/expression" }, "message": { "type": "string" } }, "required": ["expression", "message"], "additionalProperties": false } } }, "required": ["rules"], "additionalProperties": false } }, "required": ["type", "params"]
        },
        {
          "properties": { "type": { "const": "Request" }, "params": { "type": "object", "properties": { "requestName": { "type": "string" } }, "required": ["requestName"], "additionalProperties": false } }, "required": ["type", "params"]
        },
        {
          "properties": { "type": { "const": "SetState" }, "params": { "type": "object", "properties": { "key": { "type": "string" }, "value": { "$ref": "#/$defs/expression" } }, "required": ["key", "value"], "additionalProperties": false } }, "required": ["type", "params"]
        },
        {
          "properties": { "type": { "const": "CallMethod" }, "params": { "type": "object", "properties": { "methodName": { "type": "string" }, "args": { "type": "array", "items": { "$ref": "#/$defs/expression" } } }, "required": ["methodName"], "additionalProperties": false } }, "required": ["type", "params"]
        },
        {
          "properties": { "type": { "const": "CloseModal" }, "params": { "type": "object", "properties": { "result": { "$ref": "#/$defs/expression" } }, "additionalProperties": false } }, "required": ["type"]
        },
        {
          "properties": { "type": { "const": "ShowError" }, "params": { "type": "object", "properties": { "message": { "type": "string" } }, "required": ["message"], "additionalProperties": false } }, "required": ["type", "params"]
        },
        {
          "properties": { "type": { "const": "Navigate" }, "params": { "type": "object", "properties": { "url": { "type": "string" } }, "required": ["url"], "additionalProperties": false } }, "required": ["type", "params"]
        },
        {
          "properties": { "type": { "const": "Confirm" }, "params": { "type": "object", "properties": { "message": { "type": "string" }, "onConfirm": { "type": "array", "items": { "$ref": "#/$defs/action" } }, "onCancel": { "type": "array", "items": { "$ref": "#/$defs/action" } } }, "required": ["message", "onConfirm"], "additionalProperties": false } }, "required": ["type", "params"]
        }
      ],
      "additionalProperties": false
    },
    "expression": {
      "description": "Represents a value, which can be a literal, a reference to state or data, or a computed expression using an operator.",
      "oneOf": [
        { "type": "string" },
        { "type": "number" },
        { "type": "boolean" },
        { "type": "null" },
        {
          "type": "object",
          "maxProperties": 1,
          "propertyNames": { "pattern": "^_[a-zA-Z]+$" },
          "oneOf": [
            { "properties": { "_const": {} }, "required": ["_const"] },
            { "properties": { "_state": { "type": "string", "pattern": "^[a-zA-Z0-9_]+$" } }, "required": ["_state"] },
            { "properties": { "_request": { "type": "string", "pattern": "^[a-zA-Z0-9_]+\\.[a-zA-Z0-9_]+$" } }, "required": ["_request"] },
            { "properties": { "_nz": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 2 } }, "required": ["_nz"] },
            { "properties": { "_iif": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 3, "maxItems": 3 } }, "required": ["_iif"] },
            { "properties": { "_dlookup": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 3, "maxItems": 3 } }, "required": ["_dlookup"] },
            { "properties": { "_dcount": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 3 } }, "required": ["_dcount"] },
            { "properties": { "_dsum": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 3 } }, "required": ["_dsum"] },
            { "properties": { "_format": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 2 } }, "required": ["_format"] },
            { "properties": { "_add": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2 } }, "required": ["_add"] },
            { "properties": { "_sub": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 2 } }, "required": ["_sub"] },
            { "properties": { "_mul": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2 } }, "required": ["_mul"] },
            { "properties": { "_div": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 2 } }, "required": ["_div"] },
            { "properties": { "_mod": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 2 } }, "required": ["_mod"] },
            { "properties": { "_eq": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 2 } }, "required": ["_eq"] },
            { "properties": { "_ne": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 2 } }, "required": ["_ne"] },
            { "properties": { "_lt": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 2 } }, "required": ["_lt"] },
            { "properties": { "_le": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 2 } }, "required": ["_le"] },
            { "properties": { "_gt": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 2 } }, "required": ["_gt"] },
            { "properties": { "_ge": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2, "maxItems": 2 } }, "required": ["_ge"] },
            { "properties": { "_and": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2 } }, "required": ["_and"] },
            { "properties": { "_or": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 2 } }, "required": ["_or"] },
            { "properties": { "_not": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 1, "maxItems": 1 } }, "required": ["_not"] },
            { "properties": { "_isnull": { "type": "array", "items": { "$ref": "#/$defs/expression" }, "minItems": 1, "maxItems": 1 } }, "required": ["_isnull"] }
          ],
          "additionalProperties": false
        }
      ]
    },
    "block": {
      "description": "A UI element in the layout. NOTE: Cross-file reuse via `_ref` is handled by a pre-processor; this schema validates the fully resolved IR.",
      "type": "object",
      "discriminator": {
        "propertyName": "type"
      },
      "oneOf": [
        { "$ref": "#/$defs/controls/baseControl" },
        { "$ref": "#/$defs/controls/textBox" },
        { "$ref": "#/$defs/controls/label" },
        { "$ref": "#/$defs/controls/comboBox" },
        { "$ref": "#/$defs/controls/checkBox" },
        { "$ref": "#/$defs/controls/button" },
        { "$ref": "#/$defs/controls/line" },
        { "$ref": "#/$defs/containers/section" },
        { "$ref": "#/$defs/containers/row" },
        { "$ref": "#/$defs/containers/column" },
        { "$ref": "#/$defs/containers/group" }
      ],
      "properties": {
        "id": { "type": "string" },
        "visible": { "$ref": "#/$defs/expression", "default": true },
        "enabled": { "$ref": "#/$defs/expression", "default": true }
      },
      "required": ["id", "type"],
      "additionalProperties": false
    },
    "controls": {
      "baseControl": { "properties": { "type": { "type": "string" } }, "required": ["type"] },
      "textBox": {
        "properties": { "type": { "const": "TextBox" }, "label": { "type": "string" }, "placeholder": { "type": "string" }, "binding": { "oneOf": [{ "type": "string", "pattern": "^[a-zA-Z0-9_]+\\.[a-zA-Z0-9_]+$" }, { "$ref": "#/$defs/expression" }] }, "onValueChanged": { "type": "string" } }, "required": ["binding"]
      },
      "label": {
        "properties": { "type": { "const": "Label" }, "text": { "type": "string" } }, "required": ["text"]
      },
      "comboBox": {
        "properties": { "type": { "const": "ComboBox" }, "label": { "type": "string" }, "binding": { "oneOf": [{ "type": "string", "pattern": "^[a-zA-Z0-9_]+\\.[a-zA-Z0-9_]+$" }, { "$ref": "#/$defs/expression" }] }, "optionsRequest": { "type": "string" }, "valueField": { "type": "string" }, "textField": { "type": "string" }, "onValueChanged": { "type": "string" } }, "required": ["binding", "optionsRequest", "valueField", "textField"]
      },
      "checkBox": {
        "properties": { "type": { "const": "CheckBox" }, "label": { "type": "string" }, "binding": { "oneOf": [{ "type": "string", "pattern": "^[a-zA-Z0-9_]+\\.[a-zA-Z0-9_]+$" }, { "$ref": "#/$defs/expression" }] }, "onValueChanged": { "type": "string" } }, "required": ["binding"]
      },
      "button": {
        "properties": { "type": { "const": "Button" }, "text": { "type": "string" }, "onClick": { "type": "string" } }, "required": ["text", "onClick"]
      },
      "line": {
        "properties": { "type": { "const": "Line" }, "orientation": { "enum": ["horizontal", "vertical"], "default": "horizontal" } }
      }
    },
    "containers": {
      "section": {
        "properties": { "type": { "const": "Section" }, "title": { "type": "string" }, "blocks": { "type": "array", "items": { "$ref": "#/$defs/block" } } }, "required": ["blocks"]
      },
      "row": {
        "properties": { "type": { "const": "Row" }, "blocks": { "type": "array", "items": { "$ref": "#/$defs/block" } } }, "required": ["blocks"]
      },
      "column": {
        "properties": { "type": { "const": "Column" }, "blocks": { "type": "array", "items": { "$ref": "#/$defs/block" } } }, "required": ["blocks"]
      },
      "group": {
        "properties": { "type": { "const": "Group" }, "title": { "type": "string" }, "blocks": { "type": "array", "items": { "$ref": "#/$defs/block" } } }, "required": ["blocks"]
      }
    }
  }
}
```