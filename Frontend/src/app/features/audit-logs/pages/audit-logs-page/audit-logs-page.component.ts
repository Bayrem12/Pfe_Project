import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable, Subject, forkJoin, of } from 'rxjs';
import { catchError, map, switchMap, takeUntil } from 'rxjs/operators';
import { DashboardService } from '../../../dashboard/services/dashboard.service';
import { AuditLogDto, PagedAuditLogsResponse } from '../../../dashboard/models/dashboard.model';
import { User, UserService } from '../../../../core/services/user.service';
import { ScenarioService } from '../../../../core/services/scenario.service';
import { ProjectService } from '../../../../core/services/project.service';
import { FeatureService } from '../../../../core/services/feature.service';
import { ApiService } from '../../../../core/services/api.service';

interface AuditLogViewModel extends AuditLogDto {
  parsedOldValues: Record<string, unknown> | null;
  parsedNewValues: Record<string, unknown> | null;
  userDisplay: string;
  entityDisplay: string;
  consumedInMerge?: boolean;
}

type DateRangeFilter = '7d' | '30d' | '90d' | 'all';

const ROLE_ID_TO_NAME: Record<string, string> = {
  'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa': 'Admin',
  'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb': 'Manager',
  'cccccccc-cccc-cccc-cccc-cccccccccccc': 'Tester',
  'dddddddd-dddd-dddd-dddd-dddddddddddd': 'Viewer'
};

const PROJECT_ROLE_NUMBER_TO_NAME: Record<string, string> = {
  '0': 'Admin',
  '1': 'Manager',
  '2': 'Tester',
  '3': 'Viewer'
};

@Component({
  selector: 'app-audit-logs-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './audit-logs-page.component.html'
})
export class AuditLogsPageComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  logs: AuditLogViewModel[] = [];
  filteredLogs: AuditLogViewModel[] = [];
  paginatedLogs: AuditLogViewModel[] = [];

  isLoading = false;
  errorMessage = '';

  searchTerm = '';
  selectedDateRange: DateRangeFilter = 'all';
  selectedUser = '';
  selectedAction = '';
  selectedEntity = '';

  users: string[] = [];
  actions: string[] = [];
  entities: string[] = [];

  expandedLogId: string | null = null;

  currentPage = 1;
  pageSize = 25;
  totalPages = 1;
  pages: number[] = [];

  Math = Math;

  constructor(
    private dashboardService: DashboardService,
    private userService: UserService,
    private scenarioService: ScenarioService,
    private projectService: ProjectService,
    private featureService: FeatureService,
    private apiService: ApiService
  ) {}

  ngOnInit(): void {
    this.loadAuditLogs();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAuditLogs(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.fetchAllAuditLogs()
      .pipe(
        switchMap((auditItems) => {
          return forkJoin({
            users: this.loadUsersForAuditItems(auditItems).pipe(
              catchError(() => of([] as User[]))
            ),
            entityLookup: this.buildEntityLookup(auditItems).pipe(
              catchError(() => of(new Map<string, string>()))
            )
          }).pipe(
            map(({ users, entityLookup }) => ({
              auditItems,
              users,
              entityLookup
            }))
          );
        }),
        takeUntil(this.destroy$),
        catchError((error) => {
          this.errorMessage = error?.message || 'Failed to load audit logs.';
          this.isLoading = false;
          return of({
            auditItems: [] as AuditLogDto[],
            users: [] as User[],
            entityLookup: new Map<string, string>()
          });
        })
      )
      .subscribe(({ auditItems, users, entityLookup }) => {
        const userLookup = this.buildUserLookup(users);

        const mappedLogs = auditItems
          .map(log => this.mapLog(log, userLookup, entityLookup))
          .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());

        this.logs = this.normalizeRoleChangeLogs(mappedLogs, userLookup);

        this.buildFilterOptions();
        this.applyFilters();
        this.isLoading = false;
      });
  }

  private loadUsersForAuditItems(auditItems: AuditLogDto[]): Observable<User[]> {
    const uniqueUserIds = Array.from(new Set(
      auditItems
        .map(item => item.userId)
        .filter((id): id is string => typeof id === 'string' && id.trim().length > 0)
    ));

    if (!uniqueUserIds.length) {
      return of([]);
    }

    const requests = uniqueUserIds.map((userId) =>
      this.userService.getUserById(userId).pipe(
        map((response) => response?.resultat || null),
        catchError(() => of(null))
      )
    );

    return forkJoin(requests).pipe(
      map((users) => users.filter((user): user is User => !!user))
    );
  }

  private fetchAllAuditLogs() {
    return this.dashboardService.getAuditLogs(1, 100).pipe(
      switchMap((firstPage: PagedAuditLogsResponse) => {
        const firstItems = firstPage.items || [];

        if (firstPage.totalPages <= 1) {
          return of(firstItems);
        }

        const requests = [];
        for (let page = 2; page <= firstPage.totalPages; page++) {
          requests.push(this.dashboardService.getAuditLogs(page, 100));
        }

        return forkJoin(requests).pipe(
          map((restPages) => [
            ...firstItems,
            ...restPages.flatMap(p => p.items || [])
          ])
        );
      })
    );
  }

  private mapLog(
    log: AuditLogDto,
    userLookup: Map<string, string>,
    entityLookup: Map<string, string>
  ): AuditLogViewModel {
    const entityKey = this.getEntityLookupKey(log.entityType, log.entityId);
    const lookupEntityLabel = entityLookup.get(entityKey) || '';
    const fallbackLabel = this.getFallbackEntityLabel(log.entityType, log.entityId, log.oldValues, log.newValues, userLookup);

    const entityDisplay = lookupEntityLabel && !lookupEntityLabel.toLowerCase().includes('(name unavailable)')
      ? lookupEntityLabel
      : fallbackLabel;

    return {
      ...log,
      parsedOldValues: this.parseJsonSafely(log.oldValues),
      parsedNewValues: this.parseJsonSafely(log.newValues),
      userDisplay: this.formatUser(log.userId, userLookup),
      entityDisplay
    };
  }

  private buildEntityLookup(logs: AuditLogDto[]): Observable<Map<string, string>> {
    const uniqueEntities = new Map<string, AuditLogDto>();

    logs.forEach((log) => {
      const key = this.getEntityLookupKey(log.entityType, log.entityId);
      if (!key || uniqueEntities.has(key)) {
        return;
      }

      uniqueEntities.set(key, log);
    });

    if (!uniqueEntities.size) {
      return of(new Map<string, string>());
    }

    const resolvers = Array.from(uniqueEntities.entries()).map(([key, log]) => {
      return this.resolveEntityLabel(log).pipe(
        map((label) => ({ key, label }))
      );
    });

    return forkJoin(resolvers).pipe(
      map((items) => {
        const lookup = new Map<string, string>();
        items.forEach(({ key, label }) => {
          if (label) {
            lookup.set(key, label);
          }
        });
        return lookup;
      })
    );
  }

  private resolveEntityLabel(log: AuditLogDto): Observable<string> {
    const entityId = log.entityId;
    const type = (log.entityType || '').toLowerCase();
    const fallback = this.getFallbackEntityLabel(log.entityType, entityId, log.oldValues, log.newValues);
    const parsedNew = this.parseJsonSafely(log.newValues);
    const parsedOld = this.parseJsonSafely(log.oldValues);

    if (type.includes('projectmember')) {
      const memberUserId = this.readGuidishValue(parsedNew, 'userId', 'UserId')
        || this.readGuidishValue(parsedOld, 'userId', 'UserId');
      const roleName = this.resolveRoleName(parsedNew) || this.resolveRoleName(parsedOld);

      if (memberUserId) {
        return this.userService.getUserById(memberUserId).pipe(
          map((res) => {
            const user = res?.resultat;
            const fullName = `${user?.firstName || ''} ${user?.lastName || ''}`.trim();
            const label = fullName || user?.email;
            if (!label) return fallback;
            return roleName ? `${label} (${roleName})` : label;
          }),
          catchError(() => of(fallback))
        );
      }
    }

    if (type.includes('userrole')) {
      const memberUserId = this.readGuidishValue(parsedNew, 'userId', 'UserId')
        || this.readGuidishValue(parsedOld, 'userId', 'UserId');
      const roleName = this.resolveRoleName(parsedNew) || this.resolveRoleName(parsedOld);

      if (memberUserId) {
        return this.userService.getUserById(memberUserId).pipe(
          map((res) => {
            const user = res?.resultat;
            const fullName = `${user?.firstName || ''} ${user?.lastName || ''}`.trim();
            const label = fullName || user?.email;
            if (!label) return fallback;
            return roleName ? `${label} (${roleName})` : `${label} (Role updated)`;
          }),
          catchError(() => of(fallback))
        );
      }
    }

    if (type.includes('testsuitescenario')) {
      const scenarioId = this.readGuidishValue(parsedNew, 'scenarioId', 'ScenarioId')
        || this.readGuidishValue(parsedOld, 'scenarioId', 'ScenarioId');

      if (scenarioId) {
        return this.scenarioService.getScenarioById(scenarioId).pipe(
          map((res) => {
            const title = res?.resultat?.title;
            return title ? `Suite item: ${title}` : fallback;
          }),
          catchError(() => of(fallback))
        );
      }
    }

    if (!entityId) {
      return of(fallback);
    }

    if (type.includes('scenario')) {
      return this.scenarioService.getScenarioById(entityId).pipe(
        map((res) => res?.resultat?.title || fallback),
        catchError(() => of(fallback))
      );
    }

    if (type.includes('project')) {
      return this.projectService.getProjectById(entityId).pipe(
        map((res) => res?.resultat?.name || fallback),
        catchError(() => of(fallback))
      );
    }

    if (type.includes('feature')) {
      return this.featureService.getFeatureById(entityId).pipe(
        map((res) => res?.resultat?.name || fallback),
        catchError(() => of(fallback))
      );
    }

    if (type.includes('module')) {
      return this.apiService.get<any>(`modules/${entityId}`).pipe(
        map((res) => this.pickEntityName(res?.resultat) || fallback),
        catchError(() => of(fallback))
      );
    }

    if (type.includes('test')) {
      return this.apiService.get<any>(`testExecutions/${entityId}`).pipe(
        map((res) => this.pickEntityName(res?.resultat) || fallback),
        catchError(() => this.apiService.get<any>(`testexecutions/${entityId}`).pipe(
          map((res) => this.pickEntityName(res?.resultat) || fallback),
          catchError(() => of(fallback))
        ))
      );
    }

    return of(fallback);
  }

  private getEntityLookupKey(entityType: string, entityId: string | null): string {
    if (!entityId || !entityType) {
      return '';
    }

    return `${entityType.toLowerCase()}::${entityId.toLowerCase()}`;
  }

  private getFallbackEntityLabel(
    entityType: string,
    entityId: string | null,
    oldValues?: string | null,
    newValues?: string | null,
    userLookup?: Map<string, string>
  ): string {
    const fromValues = this.extractEntityLabelFromValues(entityType, oldValues, newValues, userLookup);
    if (fromValues) {
      return fromValues;
    }

    const prettyType = this.prettifyEntityType(entityType);
    return `${prettyType} (name unavailable)`;
  }

  private prettifyEntityType(entityType: string): string {
    if (!entityType) {
      return 'Entity';
    }

    return entityType
      .replace(/[_-]+/g, ' ')
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .toLowerCase()
      .replace(/\b\w/g, (char) => char.toUpperCase())
      .trim();
  }

  private extractEntityLabelFromValues(
    entityType: string,
    oldValues?: string | null,
    newValues?: string | null,
    userLookup?: Map<string, string>
  ): string | null {
    const newObject = this.parseJsonSafely(newValues || null);
    const oldObject = this.parseJsonSafely(oldValues || null);

    const newLabel = this.pickEntityName(newObject, entityType, userLookup);
    if (newLabel) {
      return newLabel;
    }

    return this.pickEntityName(oldObject, entityType, userLookup);
  }

  private pickEntityName(payload: any, entityType?: string, userLookup?: Map<string, string>): string | null {
    if (!payload || typeof payload !== 'object') {
      return null;
    }

    const normalizedType = (entityType || '').toLowerCase();

    if (normalizedType.includes('projectmember')) {
      const embeddedUserDisplay = this.readStringValue(payload, 'userDisplayName', 'UserDisplayName');
      const memberUserId = this.readGuidishValue(payload, 'userId', 'UserId');
      const role = this.resolveRoleName(payload);
      if (memberUserId && userLookup) {
        const memberName = userLookup.get(memberUserId) || userLookup.get(memberUserId.toLowerCase());
        if (memberName) {
          return role ? `${memberName} (${role})` : memberName;
        }
      }

      if (embeddedUserDisplay) {
        return role ? `${embeddedUserDisplay} (${role})` : embeddedUserDisplay;
      }
    }

    if (normalizedType.includes('userrole')) {
      const embeddedUserDisplay = this.readStringValue(payload, 'userDisplayName', 'UserDisplayName');
      const memberUserId = this.readGuidishValue(payload, 'userId', 'UserId');
      const roleName = this.resolveRoleName(payload);
      if (memberUserId && userLookup) {
        const memberName = userLookup.get(memberUserId) || userLookup.get(memberUserId.toLowerCase());
        if (memberName) {
          return roleName ? `${memberName} (${roleName})` : `${memberName} (Role updated)`;
        }
      }

      if (embeddedUserDisplay) {
        return roleName ? `${embeddedUserDisplay} (${roleName})` : `${embeddedUserDisplay} (Role updated)`;
      }
    }

    if (normalizedType === 'user' || normalizedType.endsWith(' user')) {
      const firstName = this.readStringValue(payload, 'firstName', 'FirstName');
      const lastName = this.readStringValue(payload, 'lastName', 'LastName');
      const email = this.readStringValue(payload, 'email', 'Email');
      const fullName = `${firstName || ''} ${lastName || ''}`.trim();
      if (fullName) return fullName;
      if (email) return email;
    }

    if (normalizedType.includes('actionmapping')) {
      const intentPattern = this.readStringValue(payload, 'intentPattern', 'IntentPattern');
      const actionType = this.readStringValue(payload, 'actionType', 'ActionType');
      if (intentPattern && actionType) return `${actionType}: ${intentPattern}`;
      if (intentPattern) return intentPattern;
    }

    if (normalizedType.includes('testsuitescenario')) {
      const suiteName = this.readStringValue(payload, 'testSuiteName', 'TestSuiteName');
      const scenarioTitle = this.readStringValue(payload, 'scenarioTitle', 'ScenarioTitle', 'title', 'Title');
      if (scenarioTitle) {
        return suiteName ? `${suiteName}: ${scenarioTitle}` : `Suite item: ${scenarioTitle}`;
      }

      if (suiteName) {
        return suiteName;
      }
    }

    if (normalizedType.includes('scenarioversion')) {
      const versionNumber = this.readNumberValue(payload, 'versionNumber', 'VersionNumber');
      if (versionNumber !== null) {
        return `Version ${versionNumber}`;
      }
    }

    const candidates = [
      this.readStringValue(payload, 'name', 'Name'),
      this.readStringValue(payload, 'title', 'Title'),
      this.readStringValue(payload, 'text', 'Text'),
      this.readStringValue(payload, 'stepText', 'StepText'),
      this.readStringValue(payload, 'displayName', 'DisplayName'),
      this.readStringValue(payload, 'userDisplayName', 'UserDisplayName'),
      this.readStringValue(payload, 'roleName', 'RoleName'),
      this.readStringValue(payload, 'entityName', 'EntityName'),
      this.readStringValue(payload, 'scenarioTitle', 'ScenarioTitle'),
      this.readStringValue(payload, 'scenarioName', 'ScenarioName'),
      this.readStringValue(payload, 'intentPattern', 'IntentPattern'),
      this.readStringValue(payload, 'projectName', 'ProjectName'),
      this.readStringValue(payload, 'testSuiteName', 'TestSuiteName'),
      this.readStringValue(payload, 'featureName', 'FeatureName'),
      this.readStringValue(payload, 'moduleName', 'ModuleName'),
      this.readStringValue(payload, 'testRunName', 'TestRunName'),
      this.readStringValue(payload, 'testExecutionName', 'TestExecutionName')
    ].filter((value) => typeof value === 'string' && value.trim().length > 0) as string[];

    return candidates.length ? candidates[0] : null;
  }

  private readStringValue(payload: any, ...keys: string[]): string | null {
    for (const key of keys) {
      const value = payload?.[key];
      if (typeof value === 'string' && value.trim()) {
        return value;
      }

      if (value !== undefined && value !== null) {
        const asString = String(value).trim();
        if (asString) {
          return asString;
        }
      }
    }

    return null;
  }

  private readGuidishValue(payload: any, ...keys: string[]): string | null {
    for (const key of keys) {
      const value = payload?.[key];
      if (value === undefined || value === null) {
        continue;
      }

      const asString = String(value).trim();
      if (!asString) {
        continue;
      }

      // Accept GUID-like and any id-like token
      return asString;
    }

    return null;
  }

  private normalizeRoleChangeLogs(logs: AuditLogViewModel[], userLookup: Map<string, string>): AuditLogViewModel[] {
    const normalized: AuditLogViewModel[] = [];

    for (let i = 0; i < logs.length; i++) {
      const current = logs[i];

      if (current.consumedInMerge) {
        continue;
      }

      const isUserRole = (current.entityType || '').toLowerCase().includes('userrole');
      const isCreateOrDelete = current.action === 'Created' || current.action === 'Deleted';

      if (!isUserRole || !isCreateOrDelete) {
        normalized.push(current);
        continue;
      }

      const siblingIndex = logs.findIndex((candidate, idx) => {
        if (idx === i || candidate.consumedInMerge) return false;
        const sameType = (candidate.entityType || '').toLowerCase().includes('userrole');
        const oppositeAction = (candidate.action === 'Created' || candidate.action === 'Deleted') && candidate.action !== current.action;
        const sameActor = candidate.userId === current.userId;
        const nearTime = Math.abs(new Date(candidate.timestamp).getTime() - new Date(current.timestamp).getTime()) <= 5000;
        return sameType && oppositeAction && sameActor && nearTime;
      });

      if (siblingIndex === -1) {
        normalized.push(current);
        continue;
      }

      const sibling = logs[siblingIndex];
      current.consumedInMerge = true;
      sibling.consumedInMerge = true;

      const deletedLog = current.action === 'Deleted' ? current : sibling;
      const createdLog = current.action === 'Created' ? current : sibling;

      const deletedUserId = this.readGuidishValue(deletedLog.parsedOldValues, 'userId', 'UserId');
      const createdUserId = this.readGuidishValue(createdLog.parsedNewValues, 'userId', 'UserId');
      const userIdForLabel = createdUserId || deletedUserId;
      const userName = userIdForLabel ? (userLookup.get(userIdForLabel) || userLookup.get(userIdForLabel.toLowerCase())) : null;

      const oldRole = this.resolveRoleName(deletedLog.parsedOldValues) || 'Previous role';
      const newRole = this.resolveRoleName(createdLog.parsedNewValues) || 'New role';

      normalized.push({
        ...createdLog,
        action: 'Updated',
        entityDisplay: userName ? `${userName} (${oldRole} -> ${newRole})` : `Role change (${oldRole} -> ${newRole})`,
        parsedOldValues: deletedLog.parsedOldValues,
        parsedNewValues: createdLog.parsedNewValues,
        oldValues: deletedLog.oldValues,
        newValues: createdLog.newValues,
        timestamp: createdLog.timestamp > deletedLog.timestamp ? createdLog.timestamp : deletedLog.timestamp
      });
    }

    return normalized.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());
  }

  private readNumberValue(payload: any, ...keys: string[]): number | null {
    for (const key of keys) {
      const value = payload?.[key];
      if (typeof value === 'number' && Number.isFinite(value)) {
        return value;
      }
    }

    return null;
  }

  private resolveRoleName(payload: any): string | null {
    const explicitRole = this.readStringValue(payload, 'roleName', 'RoleName', 'role', 'Role');
    if (explicitRole) {
      const projectRoleName = PROJECT_ROLE_NUMBER_TO_NAME[explicitRole];
      if (projectRoleName) {
        return projectRoleName;
      }

      const roleAsId = ROLE_ID_TO_NAME[explicitRole.toLowerCase()];
      return roleAsId || explicitRole;
    }

    const roleId = this.readGuidishValue(payload, 'roleId', 'RoleId');
    if (roleId) {
      return ROLE_ID_TO_NAME[roleId.toLowerCase()] || null;
    }

    return null;
  }

  private buildUserLookup(users: User[]): Map<string, string> {
    const lookup = new Map<string, string>();

    users.forEach((user) => {
      const fullName = `${user.firstName || ''} ${user.lastName || ''}`.trim();
      const label = fullName || user.email || 'Unknown User';
      lookup.set(user.id, label);
      lookup.set(user.id.toLowerCase(), label);
    });

    return lookup;
  }

  private parseJsonSafely(value: string | null): Record<string, unknown> | null {
    if (!value) return null;

    try {
      const parsed = JSON.parse(value);
      return typeof parsed === 'object' && parsed !== null ? parsed : null;
    } catch {
      return null;
    }
  }

  private formatUser(raw: string, userLookup?: Map<string, string>): string {
    if (!raw) return 'Unknown User';

    if (userLookup) {
      const mapped = userLookup.get(raw) || userLookup.get(raw.toLowerCase());
      if (mapped) return mapped;
    }

    if (raw.includes('@')) {
      const namePart = raw.split('@')[0];
      return namePart
        .split(/[._-]/g)
        .map(chunk => chunk ? chunk[0].toUpperCase() + chunk.slice(1) : '')
        .join(' ')
        .trim();
    }

    return raw;
  }

  private buildFilterOptions(): void {
    this.users = [...new Set(this.logs.map(l => l.userDisplay))].sort();
    this.actions = [...new Set(this.logs.map(l => l.action))].sort();
    this.entities = [...new Set(this.logs.map(l => l.entityType))].sort();
  }

  applyFilters(): void {
    const now = new Date();

    this.filteredLogs = this.logs.filter(log => {
      const timestamp = new Date(log.timestamp);
      const dateMatch = this.matchesDateRange(timestamp, now);
      const userMatch = this.selectedUser ? log.userDisplay === this.selectedUser : true;
      const actionMatch = this.selectedAction ? log.action === this.selectedAction : true;
      const entityMatch = this.selectedEntity ? log.entityType === this.selectedEntity : true;

      const search = this.searchTerm.trim().toLowerCase();
      const searchMatch = search
        ? [
            log.userDisplay,
            log.action,
            log.entityType,
            log.entityDisplay,
            log.entityId || '',
            log.ipAddress || ''
          ].join(' ').toLowerCase().includes(search)
        : true;

      return dateMatch && userMatch && actionMatch && entityMatch && searchMatch;
    });

    this.currentPage = 1;
    this.buildPagination();
  }

  private matchesDateRange(timestamp: Date, now: Date): boolean {
    if (this.selectedDateRange === 'all') return true;

    const days = this.selectedDateRange === '7d' ? 7 : this.selectedDateRange === '30d' ? 30 : 90;
    const threshold = new Date(now);
    threshold.setDate(now.getDate() - days);

    return timestamp >= threshold;
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.selectedDateRange = 'all';
    this.selectedUser = '';
    this.selectedAction = '';
    this.selectedEntity = '';
    this.applyFilters();
  }

  private buildPagination(): void {
    this.totalPages = Math.max(1, Math.ceil(this.filteredLogs.length / this.pageSize));
    this.pages = Array.from({ length: this.totalPages }, (_, i) => i + 1);
    this.updatePaginatedLogs();
  }

  changePage(page: number): void {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
    this.updatePaginatedLogs();
  }

  private updatePaginatedLogs(): void {
    const start = (this.currentPage - 1) * this.pageSize;
    this.paginatedLogs = this.filteredLogs.slice(start, start + this.pageSize);
  }

  toggleExpanded(logId: string): void {
    this.expandedLogId = this.expandedLogId === logId ? null : logId;
  }

  isExpanded(logId: string): boolean {
    return this.expandedLogId === logId;
  }

  getActionClass(action: string): string {
    const normalized = action.toLowerCase();
    const base = 'inline-flex items-center rounded-full px-2.5 py-1 text-[10px] font-bold uppercase tracking-widest border';
    if (normalized.includes('create')) return `${base} bg-blue-50 text-blue-600 border-blue-200/60`;
    if (normalized.includes('update')) return `${base} bg-amber-50 text-amber-600 border-amber-200/60`;
    if (normalized.includes('delete')) return `${base} bg-red-50 text-red-600 border-red-200/70`;
    if (normalized.includes('execute')) return `${base} bg-violet-50 text-violet-600 border-violet-200/70`;
    if (normalized.includes('login') || normalized.includes('log')) return `${base} bg-slate-100 text-slate-600 border-slate-200/80`;
    return `${base} bg-slate-100 text-slate-600 border-slate-200/80`;
  }

  getEntityClass(entity: string): string {
    const normalized = entity.toLowerCase();
    const base = 'inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest';
    if (normalized.includes('scenario')) return `${base} bg-indigo-100 text-indigo-700`;
    if (normalized.includes('test')) return `${base} bg-violet-100 text-violet-700`;
    if (normalized.includes('project')) return `${base} bg-sky-100 text-sky-700`;
    if (normalized.includes('user')) return `${base} bg-emerald-100 text-emerald-700`;
    if (normalized.includes('feature')) return `${base} bg-fuchsia-100 text-fuchsia-700`;
    if (normalized.includes('module')) return `${base} bg-cyan-100 text-cyan-700`;
    return `${base} bg-slate-100 text-slate-700`;
  }

  getInitials(name: string): string {
    const parts = name.split(' ').filter(Boolean);
    if (!parts.length) return 'U';
    if (parts.length === 1) return parts[0][0].toUpperCase();
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }

  getVisiblePages(): number[] {
    if (this.totalPages <= 7) return this.pages;

    if (this.currentPage <= 4) return [1, 2, 3, 4, 5, -1, this.totalPages];
    if (this.currentPage >= this.totalPages - 3) {
      return [1, -1, this.totalPages - 4, this.totalPages - 3, this.totalPages - 2, this.totalPages - 1, this.totalPages];
    }

    return [1, -1, this.currentPage - 1, this.currentPage, this.currentPage + 1, -1, this.totalPages];
  }

  getJsonEntries(values: Record<string, unknown> | null): Array<{ key: string; value: string }> {
    if (!values) return [];

    return Object.entries(values).map(([key, value]) => ({
      key,
      value: typeof value === 'string' ? value : JSON.stringify(value)
    }));
  }

  exportCurrentViewCsv(): void {
    const rows = this.filteredLogs.map(log => ({
      timestamp: new Date(log.timestamp).toISOString(),
      user: log.userDisplay,
      action: log.action,
      entityType: log.entityType,
      entityName: log.entityDisplay,
      entityId: log.entityId || '',
      ipAddress: log.ipAddress,
      oldValues: log.oldValues || '',
      newValues: log.newValues || ''
    }));

    const headers = ['timestamp', 'user', 'action', 'entityType', 'entityName', 'entityId', 'ipAddress', 'oldValues', 'newValues'];
    const csv = [
      headers.join(','),
      ...rows.map(row => headers
        .map(h => `"${String(row[h as keyof typeof row] ?? '').replace(/"/g, '""')}"`)
        .join(','))
    ].join('\n');

    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `audit-logs-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }
}
