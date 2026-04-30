// src/app/features/features-management/pages/features-list-page/features-list-page.component.ts
import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FeatureService } from '../../../../core/services/feature.service';
import { ModuleService } from '../../../../core/services/module.service';
import { Module } from '../../../../core/models/module.model';
import { AuthService } from '../../../../core/services/auth.service';
import { FeatureListDTO } from '../../../../core/models/feature.model';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

@Component({
  selector: 'app-features-list-page',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './features-list-page.component.html',
  styleUrl: './features-list-page.component.scss'
})
export class FeaturesListPageComponent implements OnInit, OnDestroy {
  modules: Module[] = [];
  features: FeatureListDTO[] = [];
  selectedModuleId: string = '';
  selectedModuleName: string = '';
  projectId: string = '';
  isLoading = false;
  error = '';
  deletingId: string | null = null;
  private destroy$ = new Subject<void>();

  constructor(
    private featureService: FeatureService,
    private moduleService: ModuleService,
    private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Récupère projectId depuis query params ou parent route
    this.projectId =
      this.route.snapshot.queryParamMap.get('projectId') ||
      this.route.snapshot.paramMap.get('projectId') || '';

    if (!this.projectId && this.route.parent) {
      this.projectId = this.route.parent.snapshot.paramMap.get('id') || '';
    }

    // Pré-sélection module depuis query params (venant de feature-detail)
    const preModuleId = this.route.snapshot.queryParamMap.get('moduleId');
    const preModuleName = this.route.snapshot.queryParamMap.get('moduleName');
    if (preModuleId) {
      this.selectedModuleId = preModuleId;
      this.selectedModuleName = preModuleName || '';
    }

    if (this.projectId) {
      this.loadModules();
    }
  }

  get canWrite(): boolean {
    const user = this.authService.getCurrentUser();
    if (!user) return false;
    const role = (user.roles?.[0] || '').toLowerCase();
    return role === 'owner' || role === 'tester' || role === 'admin';
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadModules(): void {
    this.isLoading = true;
    this.moduleService.getModulesByProjectId(this.projectId).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.modules = res.resultat || [];
        if (this.modules.length > 0) {
          // Garde la pré-sélection ou prend le premier
          if (!this.selectedModuleId || !this.modules.find(m => m.id === this.selectedModuleId)) {
            this.selectedModuleId = this.modules[0].id;
            this.selectedModuleName = this.modules[0].name;
          }
          this.loadFeatures();
        }
        this.isLoading = false;
      },
      error: () => {
        this.error = 'Failed to load modules.';
        this.isLoading = false;
      }
    });
  }

  onModuleChange(moduleId: string): void {
    this.selectedModuleId = moduleId;
    const mod = this.modules.find(m => m.id === moduleId);
    this.selectedModuleName = mod?.name || '';
    this.loadFeatures();
  }

  loadFeatures(): void {
    if (!this.selectedModuleId) return;
    this.isLoading = true;
    this.error = '';
    this.featureService.getFeaturesByModule(this.selectedModuleId).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.features = res.resultat || [];
        this.isLoading = false;
      },
      error: () => {
        this.error = 'Failed to load features.';
        this.isLoading = false;
      }
    });
  }

  createFeature(): void {
    this.router.navigate(['/features/new'], {
      queryParams: {
        moduleId: this.selectedModuleId,
        moduleName: this.selectedModuleName,
        projectId: this.projectId
      }
    });
  }

  viewFeature(id: string): void {
    this.router.navigate(['/features', id]);
  }

  deleteFeature(id: string, event: Event): void {
    event.stopPropagation();
    if (!confirm('Delete this feature and all its scenarios?')) return;
    this.deletingId = id;
    this.featureService.deleteFeature(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.deletingId = null;
        this.loadFeatures();
      },
      error: () => {
        this.error = 'Failed to delete feature.';
        this.deletingId = null;
      }
    });
  }

  navigateToProject(): void {
    if (this.projectId) {
      this.router.navigate(['/projects', this.projectId]);
    } else {
      this.router.navigate(['/projects']);
    }
  }
}