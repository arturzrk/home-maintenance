# Fix: Azure DevOps Work-Item Integration

Shared REST operations against Azure DevOps used by `/polaris.fix`: fetch the work item
(Step 1), mark it active (Step 3.5), and mark it resolved (Step 10). All three share the same
base64 PAT auth and `api-version=7.0` URL construction; only the HTTP method and payload
differ. The work-item identifier is always `{id}`.

## Operation 1: Fetch Work Item (used at Step 1)

```bash
python -c "
import json, os, sys, urllib.request, base64
pat = os.environ['AZURE_DEVOPS_PAT']
org_url = '{ORG_URL}'
project = '{PROJECT}'
work_item_id = {id}
url = f'{org_url}/{project}/_apis/wit/workitems/{work_item_id}?api-version=7.0'
auth = base64.b64encode(f':{pat}'.encode()).decode()
req = urllib.request.Request(url, headers={'Authorization': f'Basic {auth}'})
try:
    resp = urllib.request.urlopen(req)
    data = json.loads(resp.read())
    fields = data.get('fields', {})
    print(json.dumps({
        'id': data['id'],
        'title': fields.get('System.Title', ''),
        'type': fields.get('System.WorkItemType', ''),
        'state': fields.get('System.State', ''),
        'description': fields.get('System.Description', ''),
        'repro_steps': fields.get('Microsoft.VSTS.TCM.ReproSteps', ''),
        'acceptance_criteria': fields.get('Microsoft.VSTS.Common.AcceptanceCriteria', ''),
        'assigned_to': fields.get('System.AssignedTo', {}).get('displayName', ''),
        'area_path': fields.get('System.AreaPath', ''),
        'iteration_path': fields.get('System.IterationPath', ''),
        'severity': fields.get('Microsoft.VSTS.Common.Severity', ''),
        'priority': fields.get('Microsoft.VSTS.Common.Priority', 0)
    }, indent=2))
except urllib.error.HTTPError as e:
    print(json.dumps({'error': f'HTTP {e.code}: {e.reason}', 'url': url}), file=sys.stderr)
    sys.exit(1)
"
```

**Error handling**:
- **404**: "Work item {id} not found in {project}. Verify the ID and project name."
- **401/403**: "Authentication failed. Check your AZURE_DEVOPS_PAT token."
- **Network error**: "Cannot reach Azure DevOps. Check your network connection."

## Operation 2: Mark Active (used at Step 3.5)

```bash
python -c "
import json, os, sys, urllib.request, base64
pat = os.environ['AZURE_DEVOPS_PAT']
org_url = '{ORG_URL}'
project = '{PROJECT}'
work_item_id = {id}
url = f'{org_url}/{project}/_apis/wit/workitems/{work_item_id}?api-version=7.0'
auth = base64.b64encode(f':{pat}'.encode()).decode()
patch = json.dumps([
    {'op': 'add', 'path': '/fields/System.State', 'value': 'Active'},
    {'op': 'add', 'path': '/fields/System.History', 'value': 'Polaris fix workflow started. Branch: fix/{id}-{kebab-title}'}
]).encode()
req = urllib.request.Request(url, data=patch, method='PATCH',
    headers={'Authorization': f'Basic {auth}', 'Content-Type': 'application/json-patch+json'})
try:
    resp = urllib.request.urlopen(req)
    print('Work item updated to Active')
except Exception as e:
    print(f'Warning: Could not update work item: {e}', file=sys.stderr)
"
```

If the update fails, log a warning and continue.

## Operation 3: Mark Resolved (used at Step 10)

Skipped entirely if `AZURE_DEVOPS_PAT` is unset (the fix has already completed; this is a
best-effort status write-back, not a blocking step):

```bash
python -c "
import json, os, sys, urllib.request, base64
pat = os.environ.get('AZURE_DEVOPS_PAT', '')
if not pat:
    print('Skipping ADO update: AZURE_DEVOPS_PAT not set')
    sys.exit(0)
org_url = '{ORG_URL}'
project = '{PROJECT}'
work_item_id = {id}
url = f'{org_url}/{project}/_apis/wit/workitems/{work_item_id}?api-version=7.0'
auth = base64.b64encode(f':{pat}'.encode()).decode()
patch = json.dumps([
    {'op': 'add', 'path': '/fields/System.State', 'value': 'Resolved'},
    {'op': 'add', 'path': '/fields/System.History',
     'value': 'Fix implemented and tests passing. Branch: fix/{id}-{kebab-title}. Awaiting review.'}
]).encode()
req = urllib.request.Request(url, data=patch, method='PATCH',
    headers={'Authorization': f'Basic {auth}', 'Content-Type': 'application/json-patch+json'})
try:
    resp = urllib.request.urlopen(req)
    print('Work item updated to Resolved')
except Exception as e:
    print(f'Warning: Could not update work item: {e}', file=sys.stderr)
"
```

If the update fails, log a warning and continue.
