---
description: Apply antiGGGGravity PREMIUM UI Branding Standards (Header, Footer, Typography) to Single, Dual, or Triple panel WPF windows.
---

### 1. Link the Resource Dictionary
Ensure the `Window.Resources` section in your XAML file includes the `Pre_BrandStyles.xaml` dictionary:

```xml
<Window.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="/antiGGGravity;component/Resources/Pre_BrandStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Window.Resources>
```

### 2. Standardize the Window Outer Border
Wrap the main contents of the window in a Border with the `PremiumBorderStyle`:

```xml
<Border Style="{StaticResource PremiumBorderStyle}">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="70"/> <!-- PremiumHeaderHeight -->
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/> <!-- Footer -->
        </Grid.RowDefinitions>
        ...
    </Grid>
</Border>
```

### 3. Add the Premium Header
Use a Border with `BrandGradientBrush` for the header (Row 0):

```xml
<Border Grid.Row="0" Background="{StaticResource BrandGradientBrush}" CornerRadius="12,12,0,0">
    <Grid Margin="25,0">
        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
            <TextBlock Style="{StaticResource CompanyLogoStyle}"/>
            <TextBlock Style="{StaticResource CompanyDividerStyle}"/>
            <StackPanel VerticalAlignment="Center">
                <TextBlock Text="PREMIUM TOOL" Style="{StaticResource PremiumTitleStyle}"/>
                <TextBlock Text="Tool subtitle or active state" Style="{StaticResource PremiumSubtitleStyle}"/>
            </StackPanel>
        </StackPanel>
        <Button HorizontalAlignment="Right" VerticalAlignment="Center" Content="✕" 
                Style="{StaticResource PremiumIconButtonStyle}" Width="30" Height="30"
                Click="Close_Click" WindowChrome.IsHitTestVisibleInChrome="True"/>
    </Grid>
</Border>
```

### 4. Content Panel Layouts

#### Single Panel
One full-width content area (25px margin). Use for simple forms.

```xml
<StackPanel Grid.Row="1" Margin="25">
    <Border Background="#FAFAFA" CornerRadius="8" BorderBrush="{StaticResource PremiumBorderBrush}" BorderThickness="1" Padding="15">
        <StackPanel>
            <TextBlock Text="Section Title" Style="{StaticResource PremiumSectionLabelStyle}" Margin="0,0,0,15"/>
            <!-- Fields here -->
        </StackPanel>
    </Border>
</StackPanel>
```
**Live example**: `Views\General\RotateElementsView.xaml`, `Views\Model\BracingParametersView.xaml`

#### Dual Panel
Two equal columns with a 20px gap. Use for settings + config or left/right workflows.

```xml
<Grid Grid.Row="1" Margin="25">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="20"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- LEFT -->
    <ScrollViewer Grid.Column="0" VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <Border Background="#FAFAFA" CornerRadius="8" BorderBrush="{StaticResource PremiumBorderBrush}" BorderThickness="1" Padding="15">
                <StackPanel>
                    <TextBlock Text="Left Panel" Style="{StaticResource PremiumSectionLabelStyle}" Margin="0,0,0,15"/>
                    <!-- Fields -->
                </StackPanel>
            </Border>
        </StackPanel>
    </ScrollViewer>

    <!-- RIGHT -->
    <ScrollViewer Grid.Column="2" VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <Border Background="#FAFAFA" CornerRadius="8" BorderBrush="{StaticResource PremiumBorderBrush}" BorderThickness="1" Padding="15">
                <StackPanel>
                    <TextBlock Text="Right Panel" Style="{StaticResource PremiumSectionLabelStyle}" Margin="0,0,0,15"/>
                    <!-- Fields -->
                </StackPanel>
            </Border>
        </StackPanel>
    </ScrollViewer>
</Grid>
```
**Live examples**: `Views\Rebar\WallCornerRebarLView.xaml`, `Views\General\JoinUnjoinView.xaml`

#### Triple Panel
Three equal columns with 20px gaps. Use for complex multi-section forms.

```xml
<Grid Grid.Row="1" Margin="25">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="20"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="20"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- Column 0 -->
    <ScrollViewer Grid.Column="0" VerticalScrollBarVisibility="Auto">
        <Border Background="#FAFAFA" CornerRadius="8" BorderBrush="{StaticResource PremiumBorderBrush}" BorderThickness="1" Padding="15">
            <StackPanel>
                <TextBlock Text="Column 1" Style="{StaticResource PremiumSectionLabelStyle}" Margin="0,0,0,15"/>
            </StackPanel>
        </Border>
    </ScrollViewer>

    <!-- Column 2 -->
    <ScrollViewer Grid.Column="2" VerticalScrollBarVisibility="Auto">
        <Border Background="#FAFAFA" CornerRadius="8" BorderBrush="{StaticResource PremiumBorderBrush}" BorderThickness="1" Padding="15">
            <StackPanel>
                <TextBlock Text="Column 2" Style="{StaticResource PremiumSectionLabelStyle}" Margin="0,0,0,15"/>
            </StackPanel>
        </Border>
    </ScrollViewer>

    <!-- Column 4 -->
    <ScrollViewer Grid.Column="4" VerticalScrollBarVisibility="Auto">
        <Border Background="#FAFAFA" CornerRadius="8" BorderBrush="{StaticResource PremiumBorderBrush}" BorderThickness="1" Padding="15">
            <StackPanel>
                <TextBlock Text="Column 3" Style="{StaticResource PremiumSectionLabelStyle}" Margin="0,0,0,15"/>
            </StackPanel>
        </Border>
    </ScrollViewer>
</Grid>
```
**Live example**: `Views\Rebar\BeamRebarView.xaml`

### 5. Setup the Standard Footer
The footer should contain status info on the left and primary actions on the right:

```xml
<Border Grid.Row="2" Padding="20,15" Background="#FAFAFA" CornerRadius="0,0,12,12" BorderBrush="{StaticResource PremiumBorderBrush}" BorderThickness="0,1,0,0">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        
        <TextBlock Text="Status..." VerticalAlignment="Center" Foreground="#999999"/>
        <Button Grid.Column="1" Content="Cancel" Style="{StaticResource PremiumPrimaryButtonStyle}" Width="90" Margin="0,0,12,0"/>
        <Button Grid.Column="2" Content="Apply" Style="{StaticResource PremiumActionButtonStyle}" Width="100"/>
    </Grid>
</Border>
```

### 6. WindowChrome Setup
Always add these Window attributes and WindowChrome element:

```xml
<Window ...
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        WindowStartupLocation="CenterScreen" ShowInTaskbar="True" Topmost="True">

    <WindowChrome.WindowChrome>
        <WindowChrome CaptionHeight="70" CornerRadius="12" GlassFrameThickness="0" ResizeBorderThickness="4"/>
    </WindowChrome.WindowChrome>
```

### 7. Available Styles Reference
| Style Key | Usage |
|---|---|
| `PremiumBorderStyle` | Outer window border (shadow, rounded corners) |
| `BrandGradientBrush` | Header gradient (blue → gold) |
| `CompanyLogoStyle` | Company name text in header |
| `CompanyDividerStyle` | Vertical divider after company name |
| `PremiumTitleStyle` | Tool title (14px SemiBold white) |
| `PremiumSubtitleStyle` | Tool subtitle (11px light) |
| `PremiumIconButtonStyle` | Close (✕) button |
| `PremiumSectionLabelStyle` | Section headers inside content |
| `PremiumBorderBrush` | Section border color |
| `PremiumActionButtonStyle` | Primary action button (gradient) |
| `PremiumPrimaryButtonStyle` | Secondary/Cancel button |
| `PremiumSecondaryButtonStyle` | Tertiary buttons (All/None etc.) |

