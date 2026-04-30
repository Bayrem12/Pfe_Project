import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

interface QuickStartStep {
  step: string;
  title: string;
  description: string;
  route: string;
  action: string;
}

interface ModuleGuide {
  icon: string;
  title: string;
  summary: string;
  route: string;
  roles: string;
  howTo: string[];
}

interface RoleGuide {
  role: string;
  accent: string;
  can: string[];
  cannot: string[];
}

interface FaqItem {
  question: string;
  answer: string;
}

interface TroubleshootItem {
  issue: string;
  solution: string;
}

@Component({
  selector: 'app-help-page',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './help-page.component.html'
})
export class HelpPageComponent {
  readonly quickStartSteps: QuickStartStep[] = [
    {
      step: '01',
      title: 'Open your dashboard',
      description: 'Start from Dashboard to see project health, test activity, and recent alerts before making any changes.',
      route: '/dashboard',
      action: 'Go to Dashboard'
    },
    {
      step: '02',
      title: 'Create project structure',
      description: 'Build your testing hierarchy by creating projects, modules, and features. This keeps scenarios organized and reusable.',
      route: '/projects',
      action: 'Go to Projects'
    },
    {
      step: '03',
      title: 'Write scenarios',
      description: 'Create BDD scenarios with clear Given/When/Then steps. Attach tags and status to improve filtering and reporting.',
      route: '/scenarios',
      action: 'Go to Scenarios'
    },
    {
      step: '04',
      title: 'Build suites and execute',
      description: 'Group scenarios into test suites, run tests, then review failures and execution details from Test Runs.',
      route: '/test-suites',
      action: 'Go to Test Suites'
    }
  ];

  readonly moduleGuides: ModuleGuide[] = [
    {
      icon: 'dashboard',
      title: 'Dashboard',
      summary: 'The control center with key metrics, alerts, and quick status visibility.',
      route: '/dashboard',
      roles: 'Owner, Manager, Tester, Viewer',
      howTo: [
        'Check top metrics first to understand overall quality and trend direction.',
        'Review recent events and failed activity before planning new runs.',
        'Use it as your starting point every day before changing data.'
      ]
    },
    {
      icon: 'folder_open',
      title: 'Projects, Modules, and Features',
      summary: 'Define your product structure so tests map cleanly to real functional areas.',
      route: '/projects',
      roles: 'Owner, Manager, Tester (Viewer has read-only access)',
      howTo: [
        'Create or open a project first.',
        'Add modules, then attach features under each module.',
        'Keep names business-focused so teams can find scenarios quickly.'
      ]
    },
    {
      icon: 'description',
      title: 'Scenarios',
      summary: 'Create, edit, filter, and maintain BDD scenarios linked to features and modules.',
      route: '/scenarios',
      roles: 'Owner and Tester can create and edit; Viewer is read-only',
      howTo: [
        'Use concise scenario titles and clean Given/When/Then structure.',
        'Add tags to support bulk filtering and targeted execution.',
        'Use bulk actions carefully when operating on multiple scenarios.'
      ]
    },
    {
      icon: 'content_copy',
      title: 'Test Suites',
      summary: 'Bundle scenarios into repeatable execution sets for regression, smoke, or release checks.',
      route: '/test-suites',
      roles: 'Owner and Tester can modify suites; Viewer is read-only',
      howTo: [
        'Create suite with clear scope name (for example: Checkout Smoke).',
        'Drag or add scenarios in the desired execution order.',
        'Save suite changes before running to keep definition consistent.'
      ]
    },
    {
      icon: 'play_circle',
      title: 'Test Runs',
      summary: 'Inspect execution history, pass rates, failures, and detailed step-level results.',
      route: '/test-runs',
      roles: 'All roles can view runs; execution rights depend on role policies',
      howTo: [
        'Filter by status to isolate failed or unstable runs quickly.',
        'Open run details and expand scenario results to inspect each step.',
        'Use logs and screenshots to diagnose root causes faster.'
      ]
    },
    {
      icon: 'auto_awesome',
      title: 'NLP Configuration',
      summary: 'Map natural language phrases to test actions and parse Gherkin text safely.',
      route: '/nlp/action-mappings',
      roles: 'Owner, Manager, Tester can manage mappings; Viewer can parse text only',
      howTo: [
        'Add intent patterns that match team language consistently.',
        'Test parser output using realistic Gherkin examples.',
        'Keep mappings specific to reduce ambiguity during parsing.'
      ]
    },
    {
      icon: 'history',
      title: 'Audit Logs',
      summary: 'Track who changed what and when for governance and debugging.',
      route: '/audit-logs',
      roles: 'Owner, Manager, Tester (Viewer access is restricted)',
      howTo: [
        'Filter by entity, user, and action to narrow investigations quickly.',
        'Expand rows to compare before/after values for exact change history.',
        'Export filtered logs when sharing incident reports.'
      ]
    },
    {
      icon: 'group',
      title: 'Users and Roles',
      summary: 'Control workspace access and assign permissions based on responsibility.',
      route: '/users',
      roles: 'Owner only for management actions',
      howTo: [
        'Invite users with clear role assignment from the beginning.',
        'Review inactive accounts regularly and adjust status when needed.',
        'Use least-privilege role assignment for safer collaboration.'
      ]
    }
  ];

  readonly roleGuides: RoleGuide[] = [
    {
      role: 'Owner',
      accent: 'border-blue-200 bg-blue-50 text-blue-900',
      can: [
        'Manage users and role assignments',
        'Create, edit, and delete projects, scenarios, and suites',
        'Run tests and access all operational pages including audit logs',
        'Configure NLP mappings and system-wide test assets'
      ],
      cannot: []
    },
    {
      role: 'Manager',
      accent: 'border-violet-200 bg-violet-50 text-violet-900',
      can: [
        'View most project and execution data',
        'Work with planning and review workflows',
        'Access audit logs based on current policy'
      ],
      cannot: [
        'Manage users (Owner-only)',
        'Perform Owner-only restricted operations'
      ]
    },
    {
      role: 'Tester',
      accent: 'border-emerald-200 bg-emerald-50 text-emerald-900',
      can: [
        'Create and update scenarios',
        'Build and run test suites',
        'Use NLP parser and manage mappings where permitted',
        'Review test runs and quality outcomes'
      ],
      cannot: [
        'Manage user roles',
        'Perform Owner-only administration actions'
      ]
    },
    {
      role: 'Viewer',
      accent: 'border-slate-200 bg-slate-50 text-slate-900',
      can: [
        'View projects, scenarios, suites, and test history in read-only mode',
        'Use NLP parser for Gherkin text analysis'
      ],
      cannot: [
        'Create, edit, delete, or run restricted actions',
        'Modify NLP mappings',
        'Access restricted pages such as audit logs (based on policy)'
      ]
    }
  ];

  readonly workflowChecklist: string[] = [
    'Create or select a project and verify modules/features are ready.',
    'Write or update scenarios with complete and consistent Gherkin steps.',
    'Tag scenarios by feature, sprint, or criticality for easier filtering.',
    'Add selected scenarios into a test suite and verify execution order.',
    'Run the suite (or run scenario-level checks) and monitor progress.',
    'Inspect Test Runs details, logs, and screenshots for failures.',
    'Use Audit Logs for traceability when behavior changed unexpectedly.',
    'Refine NLP mappings and scenarios based on repeated parser/execution issues.'
  ];

  readonly faqItems: FaqItem[] = [
    {
      question: 'Why are some buttons disabled for my account?',
      answer: 'The platform enforces role-based permissions. If a button is disabled, your current role does not allow that action. Ask an Owner to adjust access if needed.'
    },
    {
      question: 'What is the best way to structure projects?',
      answer: 'Use business domains for projects, functional areas for modules, and testable slices for features. This structure keeps scenario ownership clear and prevents duplication.'
    },
    {
      question: 'How do I reduce flaky tests?',
      answer: 'Keep steps deterministic, avoid brittle selectors, review repeated failures in Test Runs, and update scenario wording or NLP mappings when intent is ambiguous.'
    },
    {
      question: 'When should I use NLP parser versus manual scenario editing?',
      answer: 'Use NLP parser to accelerate first drafts and consistency. Use manual editing to refine edge cases, expected outcomes, and business-specific language.'
    },
    {
      question: 'How do I investigate unexpected data changes?',
      answer: 'Use Audit Logs filters by entity/user/action, expand records to compare before and after values, then cross-check timestamps with related test runs.'
    }
  ];

  readonly troubleshooting: TroubleshootItem[] = [
    {
      issue: 'I cannot access a specific page.',
      solution: 'Confirm you are logged in and verify your role has access. If not, request access from an Owner.'
    },
    {
      issue: 'Lists appear empty even though data exists.',
      solution: 'Check project filters, status filters, and search terms. Clear filters to confirm data visibility.'
    },
    {
      issue: 'Gherkin parser output is incorrect.',
      solution: 'Ensure Given/When/Then syntax is valid and update NLP mappings so intent patterns match your wording.'
    },
    {
      issue: 'Suite execution results are hard to interpret.',
      solution: 'Open Test Run details and inspect scenario-level and step-level results, then correlate with logs and screenshots.'
    }
  ];
}
