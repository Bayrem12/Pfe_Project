// src/app/features/features-management/pages/feature-detail-page/feature-detail-page.component.ts
import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { FeatureService } from '../../../../core/services/feature.service';
import { FeatureDTO } from '../../../../core/models/feature.model';
import { AuthService } from '../../../../core/services/auth.service';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-feature-detail-page',
  standalone: true,
  imports: [CommonModule, RouterModule, TranslatePipe],
  templateUrl: './feature-detail-page.component.html',
  styleUrl: './feature-detail-page.component.scss'
})
export class FeatureDetailPageComponent implements OnInit, OnDestroy {
  feature: FeatureDTO | null = null;
  featureId: string = '';
  isLoading = false;
  currentRole = 'viewer';
  showDeleteConfirm = false;
  isDeleting = false;
  private destroy$ = new Subject<void>();

  constructor(
    private featureService: FeatureService,
    private router: Router,
    private route: ActivatedRoute,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.featureId = this.route.snapshot.paramMap.get('id') || '';

    if (!this.featureId) {
      this.router.navigate(['/features']);
      return;
    }

    this.loadFeature();

    this.authService.currentUser$.pipe(takeUntil(this.destroy$)).subscribe(user => {
      this.currentRole = (user?.roles?.[0] || 'viewer').toLowerCase();
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadFeature(): void {
    this.isLoading = true;
    this.featureService.getFeatureById(this.featureId).subscribe({
      next: (response) => {
        if (response.status === 200 && response.resultat) {
          this.feature = response.resultat;
        }
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.router.navigate(['/features']);
      }
    });
  }

  canEdit(): boolean {
    return this.currentRole === 'owner' || this.currentRole === 'tester' || this.currentRole === 'admin';
  }

  navigateToEdit(): void {
    this.router.navigate(['/features', this.featureId, 'edit']);
  }

  /** Navigate to scenario list filtered by this feature */
  navigateToScenarios(): void {
    if (!this.feature) return;
    this.router.navigate(['/scenarios'], {
      queryParams: {
        featureId: this.featureId,
        featureName: this.feature.name
      }
    });
  }

  /** Navigate to create a new scenario pre-filled with featureId */
  navigateToCreateScenario(): void {
    if (!this.feature) return;
    this.router.navigate(['/scenarios/new'], {
      queryParams: {
        featureId: this.featureId,
        featureName: this.feature.name
      }
    });
  }

  navigateBack(): void {
    if (this.feature) {
      this.router.navigate(['/features'], {
        queryParams: {
          moduleId: this.feature.moduleId,
          moduleName: this.feature.moduleName
        }
      });
    } else {
      this.router.navigate(['/features']);
    }
  }

  confirmDelete(): void {
    this.showDeleteConfirm = true;
  }

  cancelDelete(): void {
    this.showDeleteConfirm = false;
  }

  deleteFeature(): void {
    if (!this.feature) return;
    this.isDeleting = true;
    this.featureService.deleteFeature(this.featureId).subscribe({
      next: () => this.navigateBack(),
      error: () => {
        this.isDeleting = false;
        this.showDeleteConfirm = false;
      }
    });
  }
}